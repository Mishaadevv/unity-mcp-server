// HTTP JSON-RPC bridge: background listener + main-thread Unity execution.
// Listens on 127.0.0.1 only. All Unity API calls run on the main thread.
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    [InitializeOnLoad]
    internal static class UnityMCPBridge
    {
        public const string Version = "0.1.0";
        private static HttpListener _listener;
        private static Thread _thread;
        private static ConsoleBuffer _console;
        private static readonly object StateLock = new();

        public static bool IsRunning => _listener != null && _listener.IsListening;
        public static int Port => EditorPrefs.GetInt("UnityMCP.Port", 6400);
        public static bool MutationsEnabled
        {
            get => EditorPrefs.GetBool("UnityMCP.Mutations", true);
            set => EditorPrefs.SetBool("UnityMCP.Mutations", value);
        }

        private static bool _userStopped;
        private static double _lastWatchdogTime;
        private static int _startFailures;
        private static double _nextRetryTime;
        private static bool _gaveUp;

        static UnityMCPBridge()
        {
            _console = new ConsoleBuffer();
            if (EditorPrefs.GetBool("UnityMCP.AutoStart", true))
                EditorApplication.delayCall += () => Start();
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;
            EditorApplication.update += Watchdog;
        }

        private static void OnBeforeReload()
        {
            _userStopped = false; // reload stop is not a user stop
            Stop();
        }

        // Self-heal: if the listener or its thread died (domain reload races,
        // background thread aborts), restart automatically with backoff.
        // Give up after 8 failures to avoid spamming the console; manual Start resets.
        private static void Watchdog()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - _lastWatchdogTime < 5.0) return;
            _lastWatchdogTime = now;
            if (_userStopped || _gaveUp || !EditorPrefs.GetBool("UnityMCP.AutoStart", true)) return;
            bool threadDead = _thread != null && !_thread.IsAlive;
            if ((IsRunning && !threadDead) || now < _nextRetryTime) return;
            if (_startFailures >= 8)
            {
                _gaveUp = true;
                Debug.LogError("[UnityMCP] Giving up auto-restart after 8 failed attempts. " +
                    "The port is likely held by a zombie listener in this process — restart the Unity Editor. " +
                    "Press Start in Window > Unity MCP to retry manually.");
                return;
            }
            Debug.Log("[UnityMCP] Watchdog: listener/thread dead, restarting.");
            Restart();
            _nextRetryTime = now + Math.Min(120.0, 5.0 * Math.Pow(2.0, _startFailures));
        }

        public static void Start()
        {
            lock (StateLock)
            {
                _userStopped = false;
                _gaveUp = false; // manual Start always retries fresh
                if (IsRunning && _thread != null && _thread.IsAlive) return;
                // Clean up any zombie listener first (rebind races after reload).
                try { _listener?.Close(); } catch { /* ignore */ }
                _listener = null;
                try
                {
                    _listener = new HttpListener();
                    _listener.Prefixes.Add($"http://127.0.0.1:{Port}/rpc/");
                    _listener.Start();
                    _thread = new Thread(ListenLoop) { IsBackground = true, Name = "UnityMCP-Bridge" };
                    _thread.Start();
                    _startFailures = 0;
                    _gaveUp = false;
                    _nextRetryTime = 0;
                    Debug.Log($"[UnityMCP] Bridge running on http://127.0.0.1:{Port}/rpc/ (v{Version})");
                }
                catch (Exception ex)
                {
                    try { _listener?.Close(); } catch { /* ignore */ }
                    _listener = null;
                    _startFailures++;
                    Debug.LogError($"[UnityMCP] Failed to start on port {Port} (attempt {_startFailures}): {ex.Message}. Change port in Window > Unity MCP.");
                }
            }
        }

        public static void Stop()
        {
            lock (StateLock)
            {
                _userStopped = true;
                try { _listener?.Stop(); } catch { /* already stopped */ }
                _listener = null;
            }
        }

        public static void Restart()
        {
            _userStopped = false;
            try { _listener?.Stop(); } catch { /* already stopped */ }
            _listener = null;
            Start();
        }

        private static void ListenLoop()
        {
            var listener = _listener;
            while (listener != null && listener.IsListening)
            {
                HttpListenerContext ctx = null;
                try { ctx = listener.GetContext(); }
                catch { break; } // stopped
                try { Handle(ctx); }
                catch (Exception ex)
                {
                    try { WriteJson(ctx, 500, new { error = ex.Message }); } catch { /* client gone */ }
                }
            }
        }

        private static void Handle(HttpListenerContext ctx)
        {
            if (ctx.Request.HttpMethod != "POST")
            {
                WriteJson(ctx, 405, new { error = "Use POST with JSON-RPC 2.0 body." });
                return;
            }

            string body;
            using (var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8))
                body = reader.ReadToEnd();
            if (body.Length > 2 * 1024 * 1024)
            {
                WriteRpcError(ctx, 0, -32700, "Request too large (max 2MB).");
                return;
            }

            JObject req;
            try { req = JObject.Parse(body); }
            catch { WriteRpcError(ctx, 0, -32700, "Invalid JSON. Send JSON-RPC 2.0 {jsonrpc,id,method,params}."); return; }

            int id = req["id"]?.ToObject<int>() ?? 0;
            string method = req["method"]?.ToObject<string>() ?? string.Empty;
            var p = req["params"] as JObject ?? new JObject();

            // Execute on main thread, wait for result (TS client times out at 30s).
            object result = null;
            Exception failure = null;
            var done = new ManualResetEventSlim(false);
            EditorThreadDispatcher.Enqueue(() =>
            {
                try { result = Dispatch(method, p); }
                catch (Exception ex) { failure = ex; }
                finally { done.Set(); }
            });

            if (!done.Wait(TimeSpan.FromSeconds(25)))
            {
                WriteRpcError(ctx, id, -32000, "Unity Editor did not respond in 25s (compiling or modal dialog?). Wait and retry.");
                return;
            }
            if (failure != null)
            {
                string hint = failure is ArgumentException
                    ? "Check object path / component / property names with unity_find_gameobjects and unity_get_components first."
                    : "See Unity console for details.";
                WriteRpcError(ctx, id, -32000, failure.Message, hint);
                return;
            }

            var payload = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["result"] = result == null ? JValue.CreateNull() : JToken.FromObject(result),
            };
            WriteJson(ctx, 200, payload);
        }

        private static object Dispatch(string method, JObject p)
        {
            switch (method)
            {
                case "ping":
                    return new Dictionary<string, object>
                    {
                        ["unityVersion"] = Application.unityVersion,
                        ["bridgeVersion"] = Version,
                    };
                case "project/info":
                    return SceneHandler.GetProjectInfo(CurrentPlayMode());
                case "scene/info":
                    return SceneHandler.GetSceneInfo();
                case "scene/hierarchy":
                    return SceneHandler.GetHierarchy(p["root_only"]?.ToObject<bool>() ?? true);
                case "scene/save":
                    RequireMutations();
                    return SceneHandler.SaveScene();

                case "object/find":
                    return GameObjectHandler.Find(
                        p["name_contains"]?.ToObject<string>(),
                        p["tag"]?.ToObject<string>(),
                        p["with_component"]?.ToObject<string>());
                case "object/info":
                    return GameObjectHandler.GetInfo(GameObjectHandler.FindByPath(Req(p, "path")));
                case "object/components":
                    return ComponentHandler.GetComponents(GameObjectHandler.FindByPath(Req(p, "path")));
                case "object/create":
                    RequireMutations();
                    var created = GameObjectHandler.Create(
                        Req(p, "name"),
                        p["primitive_type"]?.ToObject<string>() ?? "none",
                        Arr(p, "position", new[] { 0f, 0f, 0f }),
                        Arr(p, "rotation", new[] { 0f, 0f, 0f }),
                        Arr(p, "scale", new[] { 1f, 1f, 1f }),
                        p["parent_path"]?.ToObject<string>());
                    return new Dictionary<string, object>
                    {
                        ["path"] = SceneHandler.GetPath(created),
                        ["name"] = created.name,
                    };
                case "object/delete":
                    RequireMutations();
                    var del = GameObjectHandler.FindByPath(Req(p, "path"));
                    string delPath = SceneHandler.GetPath(del);
                    GameObjectHandler.Delete(del);
                    return new Dictionary<string, object> { ["deleted"] = true, ["path"] = delPath };
                case "object/setTransform":
                    RequireMutations();
                    var st = GameObjectHandler.FindByPath(Req(p, "path"));
                    GameObjectHandler.SetTransform(st,
                        OptArr(p, "position"), OptArr(p, "rotation"), OptArr(p, "scale"));
                    return GameObjectHandler.GetInfo(st);
                case "object/addComponent":
                    RequireMutations();
                    var ac = GameObjectHandler.FindByPath(Req(p, "path"));
                    var comp = ComponentHandler.AddComponent(ac, Req(p, "component_type"));
                    return new Dictionary<string, object> { ["type"] = comp.GetType().Name };
                case "object/setProperty":
                    RequireMutations();
                    var sp = GameObjectHandler.FindByPath(Req(p, "path"));
                    ComponentHandler.SetProperty(sp,
                        p["component_type"]?.ToObject<string>(),
                        Req(p, "property"),
                        p["value"]);
                    return new Dictionary<string, object> { ["ok"] = true, ["property"] = p["property"]?.ToObject<string>() };

                case "console/logs":
                    int max = p["max_logs"]?.ToObject<int>() ?? 30;
                    var types = new HashSet<string>(
                        p["log_types"]?.ToObject<string[]>() ?? new[] { "warning", "error" });
                    return _console.Query(Math.Min(Math.Max(max, 1), 100), types);
                case "console/clear":
                    _console.Clear();
                    try
                    {
                        // LogEntries is internal — resolve by name, never by typeof.
                        var logEntries = typeof(UnityEditor.EditorWindow).Assembly
                            .GetType("UnityEditor.LogEntries");
                        logEntries
                            ?.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            ?.Invoke(null, null);
                    }
                    catch { /* optional */ }
                    return new Dictionary<string, object> { ["cleared"] = true };

                case "playmode/get":
                    return new Dictionary<string, object> { ["state"] = CurrentPlayMode() };
                case "playmode/set":
                    return SetPlayMode(p["action"]?.ToObject<string>());

                case "script/list":
                    return ScriptHandler.List(
                        p["folder"]?.ToObject<string>() ?? "Assets",
                        p["filter"]?.ToObject<string>());
                case "script/read":
                    return ScriptHandler.Read(
                        Req(p, "path"),
                        p["start_line"]?.ToObject<int>() ?? 1,
                        p["max_lines"]?.ToObject<int>() ?? 200);
                case "script/create":
                    RequireMutations();
                    return ScriptHandler.Create(
                        Req(p, "path"),
                        p["template"]?.ToObject<string>() ?? "monobehaviour",
                        p["class_name"]?.ToObject<string>(),
                        p["overwrite"]?.ToObject<bool>() ?? false);
                case "script/update":
                    RequireMutations();
                    return ScriptHandler.Update(
                        Req(p, "path"),
                        Req(p, "old_text"),
                        p["new_text"]?.ToObject<string>() ?? string.Empty,
                        p["expected_occurrences"]?.ToObject<int>() ?? 1);
                case "script/attach":
                    RequireMutations();
                    var attachTarget = GameObjectHandler.FindByPath(Req(p, "object_path"));
                    return ScriptHandler.Attach(attachTarget, Req(p, "script_class"));

                case "asset/list":
                    return AssetHandler.List(
                        p["folder"]?.ToObject<string>() ?? "Assets",
                        p["filter"]?.ToObject<string>(),
                        p["type"]?.ToObject<string>());
                case "asset/import":
                    RequireMutations();
                    return AssetHandler.Import(Req(p, "path"));
                case "asset/dependencies":
                    return AssetHandler.Dependencies(
                        Req(p, "path"),
                        p["recursive"]?.ToObject<bool>() ?? true);
                case "asset/folders":
                    return AssetHandler.FolderTree(
                        p["folder"]?.ToObject<string>() ?? "Assets",
                        p["depth"]?.ToObject<int>() ?? 3);
                case "prefab/instantiate":
                    RequireMutations();
                    var inst = AssetHandler.InstantiatePrefab(
                        Req(p, "prefab_path"),
                        p["name"]?.ToObject<string>(),
                        Arr(p, "position", new[] { 0f, 0f, 0f }),
                        Arr(p, "rotation", new[] { 0f, 0f, 0f }),
                        p["parent_path"]?.ToObject<string>());
                    return new Dictionary<string, object>
                    {
                        ["path"] = SceneHandler.GetPath(inst),
                        ["name"] = inst.name,
                    };
                case "prefab/apply":
                    RequireMutations();
                    var applyTarget = GameObjectHandler.FindByPath(Req(p, "object_path"));
                    AssetHandler.ApplyPrefab(applyTarget);
                    return new Dictionary<string, object>
                    {
                        ["applied"] = true,
                        ["path"] = SceneHandler.GetPath(applyTarget),
                    };

                case "code/submit":
                    RequireMutations();
                    string jobId = CodeExecHandler.Submit(
                        p["code"]?.ToObject<string>(),
                        p["timeout_s"]?.ToObject<int>() ?? 60);
                    return new Dictionary<string, object> { ["job_id"] = jobId, ["state"] = "running" };
                case "code/result":
                    return CodeExecHandler.Result(Req(p, "job_id"));

                case "asset/refresh":
                    AssetDatabase.Refresh();
                    return new Dictionary<string, object> { ["refreshed"] = true };

                case "input/set":
                    RequireMutations();
                    TankMCPInput.Set(
                        ToFloat(p["throttle"], 0f),
                        ToFloat(p["steer"], 0f),
                        p["fire"]?.ToObject<bool>() ?? false);
                    return new Dictionary<string, object>
                    {
                        ["override"] = true,
                        ["throttle"] = TankMCPInput.throttle,
                        ["steer"] = TankMCPInput.steer,
                    };
                case "input/clear":
                    TankMCPInput.Clear();
                    return new Dictionary<string, object> { ["override"] = false };

                case "game/state":
                    return GameState();

                default:
                    throw new ArgumentException($"Unknown method '{method}'. Available: ping, project/info, scene/*, object/*, console/*, playmode/*, script/*, asset/*, prefab/*.");
            }
        }

        private static string CurrentPlayMode()
        {
            if (!EditorApplication.isPlaying) return "stopped";
            return EditorApplication.isPaused ? "paused" : "playing";
        }

        private static object SetPlayMode(string action)
        {
            switch ((action ?? string.Empty).ToLowerInvariant())
            {
                case "play":
                    EditorApplication.isPlaying = true; break;
                case "stop":
                    EditorApplication.isPlaying = false; break;
                case "pause":
                    EditorApplication.isPaused = true; break;
                case "unpause":
                    EditorApplication.isPaused = false; break;
                default:
                    throw new ArgumentException($"Unknown play-mode action '{action}'. Use play|stop|pause|unpause.");
            }
            EditorApplication.delayCall += () => { };
            return new Dictionary<string, object> { ["state"] = action };
        }

        private static void RequireMutations()
        {
            if (!MutationsEnabled)
                throw new InvalidOperationException("Mutations are disabled. Enable them in Window > Unity MCP.");
        }

        private static string Req(JObject p, string key)
        {
            var v = p[key]?.ToObject<string>();
            if (string.IsNullOrEmpty(v)) throw new ArgumentException($"'{key}' is required.");
            return v;
        }

        private static float ToFloat(JToken t, float fallback)
        {
            if (t == null || t.Type == JTokenType.Null) return fallback;
            return t.ToObject<float>();
        }

        // One-call snapshot for AI self-play: every tank's team/HP/pose/gun state.
        private static object GameState()
        {
            var tanks = new List<object>();
            foreach (var th in TankHealth.all)
            {
                if (th == null) continue;
                var t = th.transform;
                float reload = 1f, turretYaw = t.eulerAngles.y;
                var pc = th.GetComponent<TankController>();
                var ai = th.GetComponent<TankAI>();
                if (pc != null) { reload = pc.ReloadFrac(); turretYaw = pc.TurretYaw(); }
                else if (ai != null) { reload = ai.ReloadFrac(); turretYaw = ai.TurretYaw(); }
                tanks.Add(new Dictionary<string, object>
                {
                    ["name"] = th.name,
                    ["team"] = th.team,
                    ["hp"] = th.hp,
                    ["maxHP"] = th.maxHP,
                    ["alive"] = th.Alive,
                    ["position"] = new[] { t.position.x, t.position.y, t.position.z },
                    ["yaw"] = t.eulerAngles.y,
                    ["turretYaw"] = turretYaw,
                    ["reloadFrac"] = reload,
                });
            }
            return new Dictionary<string, object> { ["tanks"] = tanks };
        }

        private static float[] Arr(JObject p, string key, float[] fallback)
        {
            var t = p[key];
            if (t == null || t.Type == JTokenType.Null) return fallback;
            return t.ToObject<float[]>();
        }

        private static float[] OptArr(JObject p, string key)
        {
            var t = p[key];
            if (t == null || t.Type == JTokenType.Null) return null;
            var a = t.ToObject<float[]>();
            if (a.Length != 3) throw new ArgumentException($"'{key}' must be [x, y, z].");
            return a;
        }

        private static void WriteRpcError(HttpListenerContext ctx, int id, int code, string message, string hint = null)
        {
            var payload = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["error"] = new JObject
                {
                    ["code"] = code,
                    ["message"] = message,
                    ["hint"] = hint,
                },
            };
            WriteJson(ctx, 200, payload);
        }

        private static void WriteJson(HttpListenerContext ctx, int status, object payload)
        {
            string json = payload is JObject jo
                ? jo.ToString(Formatting.None)
                : JsonConvert.SerializeObject(payload);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "application/json";
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.OutputStream.Close();
        }
    }
}
