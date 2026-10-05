// AssetDatabase queries + prefab instantiate/apply.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class AssetHandler
    {
        private static void RequireInsideAssets(string projectRelative, string what = "Path")
        {
            string rel = (projectRelative ?? string.Empty).Replace('\\', '/');
            bool ok = rel.Equals("Assets", StringComparison.OrdinalIgnoreCase)
                || rel.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase);
            if (!ok)
                throw new ArgumentException($"{what} '{projectRelative}' must be inside Assets/.");
            if (rel.Contains(".."))
                throw new ArgumentException("Parent traversal ('..') is not allowed.");
        }

        public static List<Dictionary<string, object>> List(string folder, string filter, string type)
        {
            string rel = string.IsNullOrEmpty(folder) ? "Assets" : folder.Replace('\\', '/');
            RequireInsideAssets(rel, "Folder");

            string searchFilter = string.Empty;
            if (!string.IsNullOrEmpty(type)) searchFilter += $"t:{type} ";
            if (!string.IsNullOrEmpty(filter)) searchFilter += filter;

            var guids = AssetDatabase.FindAssets(searchFilter.Trim(), new[] { rel });
            var result = new List<Dictionary<string, object>>();
            foreach (var guid in guids.Take(500))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                string typeName = asset != null ? asset.GetType().Name : "Folder";
                long size = 0;
                try
                {
                    string abs = Path.GetFullPath(Path.Combine(
                        Directory.GetParent(Application.dataPath).FullName, path));
                    if (File.Exists(abs)) size = new FileInfo(abs).Length;
                }
                catch { /* size unknown */ }
                result.Add(new Dictionary<string, object>
                {
                    ["path"] = path,
                    ["type"] = typeName,
                    ["size"] = size,
                });
            }
            return result.OrderBy(d => (string)d["path"]).ToList();
        }

        public static Dictionary<string, object> Import(string path)
        {
            RequireInsideAssets(path);
            if (!File.Exists(ToAbsolute(path)) && !Directory.Exists(ToAbsolute(path)))
                throw new ArgumentException($"Asset '{path}' does not exist. Use unity_list_assets to browse.");
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return new Dictionary<string, object> { ["path"] = path, ["imported"] = true };
        }

        public static List<string> Dependencies(string path, bool recursive)
        {
            RequireInsideAssets(path);
            if (!File.Exists(ToAbsolute(path)))
                throw new ArgumentException($"Asset '{path}' does not exist.");
            return AssetDatabase.GetDependencies(path, recursive).OrderBy(p => p).ToList();
        }

        public static Dictionary<string, object> FolderTree(string folder, int depth)
        {
            string rel = string.IsNullOrEmpty(folder) ? "Assets" : folder.Replace('\\', '/');
            RequireInsideAssets(rel, "Folder");
            string abs = ToAbsolute(rel);
            if (!Directory.Exists(abs))
                throw new ArgumentException($"Folder '{rel}' does not exist.");
            int count = 0;
            return BuildNode(rel, abs, Math.Min(Math.Max(depth, 1), 5), ref count);
        }

        private static Dictionary<string, object> BuildNode(string rel, string abs, int depthLeft, ref int count)
        {
            var node = new Dictionary<string, object> { ["name"] = Path.GetFileName(rel) };
            if (depthLeft <= 0) { node["truncated"] = true; return node; }

            var children = new List<object>();
            bool truncated = false;
            try
            {
                foreach (var dir in Directory.GetDirectories(abs).OrderBy(d => d))
                {
                    if (Path.GetFileName(dir).EndsWith(".meta")) continue;
                    if (count++ > 500) { truncated = true; break; }
                    string childRel = rel + "/" + Path.GetFileName(dir);
                    children.Add(BuildNode(childRel, dir, depthLeft - 1, ref count));
                }
                if (!truncated)
                {
                    var files = Directory.GetFiles(abs).OrderBy(f => f)
                        .Where(f => !f.EndsWith(".meta") && !f.EndsWith(".DS_Store"))
                        .Take(50)
                        .Select(f => (object)Path.GetFileName(f));
                    children.AddRange(files);
                    if (Directory.GetFiles(abs).Length > 50) truncated = true;
                }
            }
            catch (Exception ex)
            {
                children.Add($"<unreadable: {ex.Message}>");
            }
            node["children"] = children;
            if (truncated) node["truncated"] = true;
            return node;
        }

        public static GameObject InstantiatePrefab(string prefabPath, string name, float[] pos, float[] rot, string parentPath)
        {
            RequireInsideAssets(prefabPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                throw new ArgumentException(
                    $"Prefab '{prefabPath}' not found or is not a GameObject prefab. Use unity_list_assets with type='Prefab'.");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (instance == null)
                throw new InvalidOperationException($"Failed to instantiate '{prefabPath}'.");
            Undo.RegisterCreatedObjectUndo(instance, $"MCP Instantiate {prefab.name}");

            if (!string.IsNullOrEmpty(name)) instance.name = name;
            if (!string.IsNullOrEmpty(parentPath))
            {
                var parent = GameObjectHandler.FindByPath(parentPath);
                Undo.SetTransformParent(instance.transform, parent.transform, "MCP Parent prefab");
            }
            instance.transform.position = new Vector3(pos[0], pos[1], pos[2]);
            instance.transform.rotation = Quaternion.Euler(rot[0], rot[1], rot[2]);

            Selection.activeObject = instance;
            EditorUtility.SetDirty(instance);
            return instance;
        }

        public static void ApplyPrefab(GameObject go)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(go))
                throw new ArgumentException(
                    $"'{SceneHandler.GetPath(go)}' is not a prefab instance. Only instances can be applied back to their source.");
            PrefabUtility.ApplyPrefabInstance(go, InteractionMode.UserAction);
        }

        private static string ToAbsolute(string projectRelative)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, projectRelative));
        }
    }
}
