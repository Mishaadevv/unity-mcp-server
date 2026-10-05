// Window > Unity MCP: status, port, mutations toggle, MCP client config.
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal sealed class UnityMCPWindow : EditorWindow
    {
        [MenuItem("Window/Unity MCP")]
        public static void Open()
        {
            GetWindow<UnityMCPWindow>("Unity MCP");
        }

        private void OnGUI()
        {
            GUILayout.Label("Unity MCP Bridge", EditorStyles.boldLabel);
            GUILayout.Label(
                UnityMCPBridge.IsRunning
                    ? $"Status: Running on 127.0.0.1:{UnityMCPBridge.Port}"
                    : "Status: Stopped",
                EditorStyles.helpBox);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Start")) UnityMCPBridge.Start();
            if (GUILayout.Button("Stop")) UnityMCPBridge.Stop();
            if (GUILayout.Button("Restart")) UnityMCPBridge.Restart();
            GUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            int port = EditorGUILayout.IntField("Port", UnityMCPBridge.Port);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetInt("UnityMCP.Port", Mathf.Clamp(port, 1024, 65535));
                UnityMCPBridge.Restart();
            }

            bool auto = EditorPrefs.GetBool("UnityMCP.AutoStart", true);
            bool nextAuto = EditorGUILayout.Toggle("Auto-start on load", auto);
            if (nextAuto != auto) EditorPrefs.SetBool("UnityMCP.AutoStart", nextAuto);

            bool mut = UnityMCPBridge.MutationsEnabled;
            bool nextMut = EditorGUILayout.Toggle("Enable mutations (create/delete/edit)", mut);
            if (nextMut != mut) UnityMCPBridge.MutationsEnabled = nextMut;

            bool code = CodeExecHandler.Enabled;
            bool nextCode = EditorGUILayout.Toggle("Enable C# execution (sandboxed)", code);
            if (nextCode != code) CodeExecHandler.Enabled = nextCode;

            GUILayout.Space(8);
            GUILayout.Label("MCP client config (Claude Code / Cursor / stdio):", EditorStyles.boldLabel);
            string config =
                "{\n" +
                "  \"mcpServers\": {\n" +
                "    \"unity\": {\n" +
                "      \"command\": \"node\",\n" +
                $"      \"args\": [\"<path-to>/Unity-MCP/dist/index.js\"],\n" +
                "      \"env\": {\n" +
                $"        \"UNITY_BRIDGE_PORT\": \"{UnityMCPBridge.Port}\"\n" +
                "      }\n" +
                "    }\n" +
                "  }\n" +
                "}";
            EditorGUILayout.TextArea(config, GUILayout.Height(130));
            if (GUILayout.Button("Copy config")) EditorGUIUtility.systemCopyBuffer = config;

            GUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Bridge listens on 127.0.0.1 only. Mutations use Undo (Ctrl+Z reverts). " +
                "If tools report 'unreachable', press Restart and match UNITY_BRIDGE_PORT.",
                MessageType.Info);
        }
    }
}
