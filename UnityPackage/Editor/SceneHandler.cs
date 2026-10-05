// Scene-level queries: info, hierarchy, save.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityMCP.Editor
{
    internal static class SceneHandler
    {
        public static object GetSceneInfo()
        {
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            return new Dictionary<string, object>
            {
                ["name"] = scene.name,
                ["path"] = scene.path,
                ["isDirty"] = scene.isDirty,
                ["rootCount"] = roots.Length,
                ["buildIndex"] = scene.buildIndex,
            };
        }

        public static object GetProjectInfo(string playMode)
        {
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            return new Dictionary<string, object>
            {
                ["projectName"] = Application.productName,
                ["projectPath"] = System.IO.Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath,
                ["unityVersion"] = Application.unityVersion,
                ["activeScene"] = new Dictionary<string, object>
                {
                    ["name"] = scene.name,
                    ["path"] = scene.path,
                    ["isDirty"] = scene.isDirty,
                    ["rootCount"] = roots.Length,
                    ["buildIndex"] = scene.buildIndex,
                },
                ["playMode"] = playMode,
            };
        }

        public static List<Dictionary<string, object>> GetHierarchy(bool rootOnly)
        {
            var scene = SceneManager.GetActiveScene();
            var result = new List<Dictionary<string, object>>();
            foreach (var root in scene.GetRootGameObjects().OrderBy(g => g.name))
            {
                result.Add(SerializeNode(root, rootOnly));
            }
            return result;
        }

        public static Dictionary<string, object> SerializeNode(GameObject go, bool includeChildren)
        {
            var comps = go.GetComponents<Component>()
                .Where(c => c != null)
                .Select(c => c.GetType().Name)
                .ToList();
            var node = new Dictionary<string, object>
            {
                ["name"] = go.name,
                ["path"] = GetPath(go),
                ["active"] = go.activeSelf,
                ["tag"] = go.tag ?? "Untagged",
                ["layer"] = go.layer,
                ["childCount"] = go.transform.childCount,
                ["components"] = comps,
            };
            if (includeChildren)
            {
                var children = new List<Dictionary<string, object>>();
                foreach (Transform child in go.transform)
                    children.Add(SerializeNode(child.gameObject, false));
                node["children"] = children;
            }
            return node;
        }

        public static string GetPath(GameObject go)
        {
            var parts = new List<string> { go.name };
            var t = go.transform.parent;
            while (t != null)
            {
                parts.Add(t.name);
                t = t.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        public static object SaveScene()
        {
            bool saved = EditorSceneManager.SaveOpenScenes();
            var scene = SceneManager.GetActiveScene();
            return new Dictionary<string, object>
            {
                ["saved"] = saved,
                ["path"] = scene.path,
            };
        }
    }
}
