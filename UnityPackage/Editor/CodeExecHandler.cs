// Sandboxed async C# execution via AssemblyBuilder.
// Disabled by default — enable in Window > Unity MCP. Mutations toggle also required.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class CodeExecHandler
    {
        public static bool Enabled
        {
            get => EditorPrefs.GetBool("UnityMCP.CodeExec", false);
            set => EditorPrefs.SetBool("UnityMCP.CodeExec", value);
        }

        private static readonly object Lock = new();
        private static readonly Dictionary<string, CodeJob> Jobs = new();

        // Substring blocklist (case-insensitive) checked before compile.
        private static readonly string[] BlockedPatterns = new[]
        {
            "System.IO", "System.Diagnostics", "System.Net", "System.Reflection",
            "DllImport", "LoadFrom", "LoadFile", "Activator",
            "Application.Quit", "EditorApplication.Exit",
            "Process", "Socket", "HttpClient", "UnityWebRequest",
            "File.", "Directory.", "while(true)", "while (true)", "for(;;)", "for (;;)",
        };

        public static string Submit(string code, int timeoutSec)
        {
            if (!Enabled)
                throw new InvalidOperationException(
                    "C# execution is disabled. Enable 'Enable C# execution' in Window > Unity MCP first.");
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("code is required.");
            if (code.Length > 20000)
                throw new ArgumentException("code exceeds 20000 characters. Split into smaller steps.");
            foreach (var bad in BlockedPatterns)
            {
                if (code.IndexOf(bad, StringComparison.OrdinalIgnoreCase) >= 0)
                    throw new ArgumentException(
                        $"Blocked pattern '{bad}' (sandbox). Restructure without filesystem/network/process/reflection-load APIs.");
            }

            string id = Guid.NewGuid().ToString("N").Substring(0, 8);
            var job = new CodeJob
            {
                Id = id,
                State = "running",
                TimeoutSec = Math.Min(Math.Max(timeoutSec, 10), 300),
            };
            lock (Lock)
            {
                Jobs[id] = job;
                while (Jobs.Count > 50)
                {
                    var oldest = Jobs.OrderBy(kv => kv.Value.SubmittedUtc).First().Key;
                    Jobs.Remove(oldest);
                }
            }

            string dir = Path.Combine(Path.GetTempPath(), "UnityMCP", id);
            Directory.CreateDirectory(dir);
            string srcPath = Path.Combine(dir, $"__MCPJob_{id}.cs");
            string dllPath = Path.Combine(dir, $"__MCPJob_{id}.dll");
            File.WriteAllText(srcPath, Wrap(id, code), new UTF8Encoding(false));

            var builder = new AssemblyBuilder(dllPath, new[] { srcPath });
            builder.additionalReferences = DefaultReferences();
            builder.buildFinished += (assemblyPath, messages) => OnBuilt(id, assemblyPath, messages);
            if (!builder.Build())
                Fail(id, "AssemblyBuilder refused to start. Check the Unity console.");

            // Watchdog only marks the state; it cannot preempt the main thread.
            // Note: Timeout.InfiniteTimeSpan is .NET Core only — use int ms + Timeout.Infinite here.
            int watchdogMs = (job.TimeoutSec + 15) * 1000;
            var watchdog = new Timer(_ =>
            {
                lock (Lock)
                {
                    if (Jobs.TryGetValue(id, out var j) && j.State == "running")
                    {
                        j.State = "error";
                        j.Error = $"Timed out after {j.TimeoutSec}s (compile or run hung). Keep snippets short; avoid loops.";
                    }
                }
            }, null, watchdogMs, Timeout.Infinite);
            job.Watchdog = watchdog;

            return id;
        }

        public static Dictionary<string, object> Result(string jobId)
        {
            lock (Lock)
            {
                if (!Jobs.TryGetValue(jobId, out var job))
                    throw new ArgumentException($"Job '{jobId}' not found. Submit code with unity_execute_code first.");
                var d = new Dictionary<string, object>
                {
                    ["job_id"] = jobId,
                    ["state"] = job.State,
                };
                if (job.State == "done")
                {
                    d["output"] = job.Output;
                    d["logs"] = job.Logs;
                }
                else if (job.State == "error")
                {
                    d["error"] = job.Error;
                    d["logs"] = job.Logs;
                }
                return d;
            }
        }

        private static void OnBuilt(string id, string assemblyPath, CompilerMessage[] messages)
        {
            // May arrive on a worker thread — marshal everything Unity-related.
            EditorThreadDispatcher.Enqueue(() =>
            {
                CodeJob job;
                lock (Lock) { Jobs.TryGetValue(id, out job); }
                if (job == null || job.State != "running") return;

                var errors = messages
                    .Where(m => m.type == CompilerMessageType.Error)
                    .Select(m => $"{m.file}({m.line}): {m.message}")
                    .ToList();
                if (errors.Count > 0)
                {
                    Fail(id, "Compile errors:\n" + string.Join("\n", errors.Take(10)));
                    return;
                }

                var logs = new List<string>();
                void Capture(string message, string stack, LogType type)
                {
                    if (type == LogType.Log || type == LogType.Warning)
                        logs.Add($"[{type}] {message}");
                }

                try
                {
                    var asm = System.Reflection.Assembly.LoadFrom(assemblyPath);
                    var type = asm.GetType($"__MCPJob_{id}");
                    var run = type?.GetMethod("Run", BindingFlags.Static | BindingFlags.Public);
                    if (run == null) throw new InvalidOperationException("Entry Run() not found in built assembly.");

                    Application.logMessageReceived += Capture;
                    object output;
                    try
                    {
                        output = run.Invoke(null, null);
                    }
                    finally
                    {
                        Application.logMessageReceived -= Capture;
                    }

                    string outputText;
                    try
                    {
                        outputText = output == null ? "null" : JToken.FromObject(output).ToString(Newtonsoft.Json.Formatting.None);
                    }
                    catch
                    {
                        outputText = output?.ToString() ?? "null";
                    }

                    lock (Lock)
                    {
                        if (Jobs.TryGetValue(id, out var j) && j.State == "running")
                        {
                            j.State = "done";
                            j.Output = outputText.Length > 8000 ? outputText.Substring(0, 8000) + "…[truncated]" : outputText;
                            j.Logs = logs.Take(20).ToList();
                            j.Watchdog?.Dispose();
                        }
                    }
                }
                catch (Exception ex)
                {
                    var inner = ex is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : ex;
                    lock (Lock)
                    {
                        if (Jobs.TryGetValue(id, out var j) && j.State == "running")
                        {
                            j.Logs = logs.Take(20).ToList();
                        }
                    }
                    Fail(id, $"Runtime error: {inner.GetType().Name}: {inner.Message}");
                }
            });
        }

        private static void Fail(string id, string error)
        {
            lock (Lock)
            {
                if (Jobs.TryGetValue(id, out var job) && job.State == "running")
                {
                    job.State = "error";
                    job.Error = error.Length > 2000 ? error.Substring(0, 2000) + "…[truncated]" : error;
                    job.Watchdog?.Dispose();
                }
            }
        }

        private static string Wrap(string id, string code)
        {
            return
                "using UnityEngine;\nusing UnityEditor;\nusing System;\nusing System.Collections.Generic;\nusing System.Linq;\n" +
                $"public static class __MCPJob_{id}\n{{\n    public static object Run()\n    {{\n{code}\n        return null;\n    }}\n}}\n";
        }

#pragma warning disable UAC0007 // Assembly.Location guarded below (IsNullOrEmpty + File.Exists)
        private static string[] DefaultReferences()
        {
            var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void Add(System.Reflection.Assembly a)
            {
                try
                {
                    if (a != null && !a.IsDynamic && !string.IsNullOrEmpty(a.Location) && File.Exists(a.Location))
                        refs.Add(a.Location);
                }
                catch { /* ignore dynamic/in-memory assemblies */ }
            }
            Add(typeof(GameObject).Assembly);          // UnityEngine
            Add(typeof(EditorWindow).Assembly);        // UnityEditor
            Add(typeof(object).Assembly);              // mscorlib / netstandard
            Add(typeof(Enumerable).Assembly);          // System.Core (LINQ)
            Add(typeof(JToken).Assembly);              // Newtonsoft.Json
            return refs.ToArray();
        }
#pragma warning restore UAC0007

        private sealed class CodeJob
        {
            public string Id;
            public string State; // running | done | error
            public string Output = string.Empty;
            public string Error = string.Empty;
            public List<string> Logs = new();
            public int TimeoutSec = 60;
            public DateTime SubmittedUtc = DateTime.UtcNow;
            public Timer Watchdog;
        }
    }
}
