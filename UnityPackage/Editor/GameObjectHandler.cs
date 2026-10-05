// GameObject find/create/delete/transform. All mutations use Undo.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityMCP.Editor
{
    internal static class GameObjectHandler
    {
        /// <summary>
        /// Find by hierarchy path, including inactive objects.
        /// Throws ArgumentException when not found (bridge maps to JSON-RPC error).
        /// </summary>
        public static GameObject FindByPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path is required, e.g. 'Player' or 'Level/Enemies/Orc'.");

            var scene = SceneManager.GetActiveScene();
            var parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            GameObject current = null;

            IEnumerable<GameObject> candidates = scene.GetRootGameObjects()
                .Where(g => g.name == parts[0]);
            current = candidates.FirstOrDefault();
            if (current == null)
                throw new ArgumentException($"GameObject '{path}' not found. Use unity_find_gameobjects to list candidates.");

            for (int i = 1; i < parts.Length; i++)
            {
                var next = current.transform.Find(parts[i]);
                if (next == null)
                {
                    // transform.Find skips inactive? Fall back to manual scan.
                    next = ScanChildren(current.transform, parts[i]);
                    if (next == null)
                        throw new ArgumentException($"GameObject '{path}' not found at segment '{parts[i]}'.");
                }
                current = next.gameObject;
            }
            return current;
        }

        private static Transform ScanChildren(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
            }
            return null;
        }

        public static List<Dictionary<string, object>> Find(string nameContains, string tag, string withComponent)
        {
            var scene = SceneManager.GetActiveScene();
#if UNITY_6000_0_OR_NEWER
            var all = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
#else
            var all = UnityEngine.Object.FindObjectsOfType<GameObject>();
#endif
            var query = all.Where(g => g.scene == scene);

            if (!string.IsNullOrEmpty(nameContains))
                query = query.Where(g => g.name.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!string.IsNullOrEmpty(tag))
                query = query.Where(g => g.CompareTag(tag));
            if (!string.IsNullOrEmpty(withComponent))
                query = query.Where(g => g.GetComponent(withComponent) != null);

            return query
                .OrderBy(g => g.name)
                .Take(200)
                .Select(g => new Dictionary<string, object>
                {
                    ["name"] = g.name,
                    ["path"] = SceneHandler.GetPath(g),
                    ["active"] = g.activeSelf,
                    ["tag"] = g.tag,
                    ["layer"] = g.layer,
                    ["childCount"] = g.transform.childCount,
                    ["components"] = g.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name).ToList(),
                })
                .ToList();
        }

        public static Dictionary<string, object> GetInfo(GameObject go)
        {
            var t = go.transform;
            var comps = go.GetComponents<Component>()
                .Where(c => c != null)
                .Select(c =>
                {
                    var d = new Dictionary<string, object> { ["type"] = c.GetType().Name };
                    if (c is Behaviour b) d["enabled"] = b.enabled;
                    return d;
                })
                .ToList();

            return new Dictionary<string, object>
            {
                ["name"] = go.name,
                ["path"] = SceneHandler.GetPath(go),
                ["active"] = go.activeSelf,
                ["tag"] = go.tag,
                ["layer"] = go.layer,
                ["transform"] = new Dictionary<string, object>
                {
                    ["position"] = Vec(t.position),
                    ["rotationEuler"] = Vec(t.rotation.eulerAngles),
                    ["scale"] = Vec(t.localScale),
                },
                ["components"] = comps,
            };
        }

        private static Dictionary<string, object> Vec(Vector3 v)
        {
            return new Dictionary<string, object> { ["x"] = v.x, ["y"] = v.y, ["z"] = v.z };
        }

        public static GameObject Create(string name, string primitiveType, float[] pos, float[] rot, float[] scl, string parentPath)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("name is required.");

            GameObject go;
            var pt = (primitiveType ?? "none").ToLowerInvariant();
            go = pt switch
            {
                "cube" => GameObject.CreatePrimitive(PrimitiveType.Cube),
                "sphere" => GameObject.CreatePrimitive(PrimitiveType.Sphere),
                "capsule" => GameObject.CreatePrimitive(PrimitiveType.Capsule),
                "cylinder" => GameObject.CreatePrimitive(PrimitiveType.Cylinder),
                "plane" => GameObject.CreatePrimitive(PrimitiveType.Plane),
                "quad" => GameObject.CreatePrimitive(PrimitiveType.Quad),
                _ => new GameObject(name),
            };
            if (pt != "none") go.name = name;

            Undo.RegisterCreatedObjectUndo(go, $"MCP Create {name}");

            if (!string.IsNullOrEmpty(parentPath))
            {
                var parent = FindByPath(parentPath);
                Undo.SetTransformParent(go.transform, parent.transform, $"MCP Parent {name}");
            }

            go.transform.position = new Vector3(pos[0], pos[1], pos[2]);
            go.transform.rotation = Quaternion.Euler(rot[0], rot[1], rot[2]);
            go.transform.localScale = new Vector3(scl[0], scl[1], scl[2]);

            Selection.activeObject = go;
            EditorUtility.SetDirty(go);
            return go;
        }

        public static void Delete(GameObject go)
        {
            Undo.DestroyObjectImmediate(go);
        }

        public static void SetTransform(GameObject go, float[] pos, float[] rot, float[] scl)
        {
            Undo.RecordObject(go.transform, "MCP Set Transform");
            if (pos != null) go.transform.position = new Vector3(pos[0], pos[1], pos[2]);
            if (rot != null) go.transform.rotation = Quaternion.Euler(rot[0], rot[1], rot[2]);
            if (scl != null) go.transform.localScale = new Vector3(scl[0], scl[1], scl[2]);
            EditorUtility.SetDirty(go);
        }
    }
}
