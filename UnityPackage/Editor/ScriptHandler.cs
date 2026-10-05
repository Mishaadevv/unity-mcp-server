// C# script list/read/create/update/attach. File writes go through AssetDatabase.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class ScriptHandler
    {
        private static readonly UTF8Encoding Utf8NoBom = new(false);

        private static string ToAbsolute(string projectRelative)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, projectRelative));
        }

        private static void RequireInsideAssets(string projectRelative)
        {
            string rel = (projectRelative ?? string.Empty).Replace('\\', '/');
            bool ok = rel.Equals("Assets", StringComparison.OrdinalIgnoreCase)
                || rel.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase);
            if (!ok)
                throw new ArgumentException($"Path '{projectRelative}' must be inside Assets/, e.g. 'Assets/Scripts/Player.cs'.");
            if (rel.Contains(".."))
                throw new ArgumentException("Parent traversal ('..') is not allowed.");
        }

        public static List<Dictionary<string, object>> List(string folder, string filter)
        {
            string rel = string.IsNullOrEmpty(folder) ? "Assets" : folder.Replace('\\', '/');
            RequireInsideAssets(rel);
            string abs = ToAbsolute(rel);
            if (!Directory.Exists(abs))
                throw new ArgumentException($"Folder '{rel}' does not exist.");

            return Directory.GetFiles(abs, "*.cs", SearchOption.AllDirectories)
                .Select(f =>
                {
                    string full = f.Replace('\\', '/');
                    int idx = full.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase);
                    return idx >= 0 ? full.Substring(idx + 1) : full;
                })
                .Where(p => string.IsNullOrEmpty(filter) ||
                            Path.GetFileName(p).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(p => p)
                .Take(500)
                .Select(p =>
                {
                    var info = new FileInfo(ToAbsolute(p));
                    return new Dictionary<string, object>
                    {
                        ["path"] = p,
                        ["size"] = info.Exists ? info.Length : 0,
                        ["modified"] = info.Exists ? info.LastWriteTimeUtc.ToString("o") : string.Empty,
                    };
                })
                .ToList();
        }

        public static Dictionary<string, object> Read(string path, int startLine, int maxLines)
        {
            RequireInsideAssets(path);
            string abs = ToAbsolute(path);
            if (!File.Exists(abs))
                throw new ArgumentException($"Script '{path}' not found. Use unity_list_scripts to browse.");
            var lines = File.ReadAllLines(abs, Encoding.UTF8);
            int start = Math.Max(1, startLine);
            int take = Math.Min(Math.Max(1, maxLines), 2000);
            var slice = lines.Skip(start - 1).Take(take)
                .Select((code, i) => $"{start + i}: {code}");
            return new Dictionary<string, object>
            {
                ["path"] = path,
                ["total_lines"] = lines.Length,
                ["start_line"] = start,
                ["content"] = string.Join("\n", slice),
            };
        }

        public static Dictionary<string, object> Create(string path, string template, string className, bool overwrite)
        {
            RequireInsideAssets(path);
            if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"Script path must end with .cs, got '{path}'.");
            string abs = ToAbsolute(path);
            if (File.Exists(abs) && !overwrite)
                throw new ArgumentException($"'{path}' already exists. Pass overwrite=true or pick another name.");

            string cls = string.IsNullOrEmpty(className)
                ? Path.GetFileNameWithoutExtension(abs)
                : className;
            if (!IsValidIdentifier(cls))
                throw new ArgumentException($"Class name '{cls}' is not a valid C# identifier.");
            if (cls != Path.GetFileNameWithoutExtension(abs))
                throw new ArgumentException($"Class name '{cls}' must match file name '{Path.GetFileNameWithoutExtension(abs)}'.");

            string content = BuildTemplate((template ?? "monobehaviour").ToLowerInvariant(), cls);
            Directory.CreateDirectory(Path.GetDirectoryName(abs));
            File.WriteAllText(abs, content, Utf8NoBom);
            AssetDatabase.Refresh();
            return new Dictionary<string, object> { ["path"] = path, ["className"] = cls };
        }

        public static Dictionary<string, object> Update(string path, string oldText, string newText, int expected)
        {
            RequireInsideAssets(path);
            string abs = ToAbsolute(path);
            if (!File.Exists(abs))
                throw new ArgumentException($"Script '{path}' not found.");
            if (string.IsNullOrEmpty(oldText))
                throw new ArgumentException("old_text is required.");

            string text = File.ReadAllText(abs, Encoding.UTF8);
            int count = CountOccurrences(text, oldText);
            if (count != expected)
                throw new ArgumentException(
                    $"Found {count} occurrence(s) of old_text in '{path}', expected {expected}. " +
                    "Re-read the file with unity_read_script and copy old_text verbatim (whitespace matters).");

            text = text.Replace(oldText, newText ?? string.Empty);
            File.WriteAllText(abs, text, Utf8NoBom);
            AssetDatabase.Refresh();
            return new Dictionary<string, object>
            {
                ["path"] = path,
                ["replaced"] = expected,
                ["total_lines"] = text.Split('\n').Length,
            };
        }

        public static Dictionary<string, object> Attach(GameObject go, string scriptClass)
        {
            if (string.IsNullOrEmpty(scriptClass))
                throw new ArgumentException("script_class is required, e.g. 'PlayerController'.");
            if (EditorApplication.isCompiling)
                throw new InvalidOperationException("Unity is compiling scripts. Wait for compilation to finish, then retry.");

            var guids = AssetDatabase.FindAssets($"t:MonoScript {scriptClass}");
            MonoScript found = null;
            foreach (var guid in guids)
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                if (script != null && script.GetClass() != null && script.GetClass().Name == scriptClass)
                {
                    found = script;
                    break;
                }
            }
            if (found == null)
                throw new ArgumentException(
                    $"Script class '{scriptClass}' not found or not compiled. " +
                    "Check the class name, fix console compile errors, wait for compilation, then retry.");

            var type = found.GetClass();
            if (go.GetComponent(type) != null)
                throw new ArgumentException($"'{go.name}' already has '{scriptClass}' attached.");
            Undo.AddComponent(go, type);
            EditorUtility.SetDirty(go);
            return new Dictionary<string, object>
            {
                ["type"] = scriptClass,
                ["object"] = SceneHandler.GetPath(go),
            };
        }

        private static int CountOccurrences(string text, string needle)
        {
            int count = 0, idx = 0;
            while ((idx = text.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
            {
                count++;
                idx += needle.Length;
            }
            return count;
        }

        private static bool IsValidIdentifier(string name)
        {
            if (string.IsNullOrEmpty(name) || char.IsDigit(name[0])) return false;
            return name.All(c => char.IsLetterOrDigit(c) || c == '_');
        }

        private static string BuildTemplate(string template, string cls)
        {
            return template switch
            {
                "scriptableobject" =>
                    "using UnityEngine;\n\n" +
                    "[CreateAssetMenu(fileName = \"" + cls + "\", menuName = \"Game/" + cls + "\")]\n" +
                    "public class " + cls + " : ScriptableObject\n{\n}\n",
                "editor" =>
                    "using UnityEditor;\nusing UnityEngine;\n\n" +
                    "[CustomEditor(typeof(" + cls + "Target))]\n" +
                    "public class " + cls + " : Editor\n{\n" +
                    "    public override void OnInspectorGUI()\n" +
                    "    {\n" +
                    "        DrawDefaultInspector();\n" +
                    "    }\n" +
                    "}\n",
                "empty" =>
                    "public class " + cls + "\n{\n}\n",
                _ =>
                    "using UnityEngine;\n\n" +
                    "public class " + cls + " : MonoBehaviour\n{\n" +
                    "    void Start()\n" +
                    "    {\n" +
                    "    }\n\n" +
                    "    void Update()\n" +
                    "    {\n" +
                    "    }\n" +
                    "}\n",
            };
        }
    }
}
