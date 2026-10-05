// Ring buffer of Unity console entries for the bridge.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal sealed class ConsoleBuffer
    {
        private readonly object _lock = new();
        private readonly LinkedList<ConsoleEntry> _entries = new();
        private const int Capacity = 200;

        public ConsoleBuffer()
        {
            Application.logMessageReceived += OnLog;
        }

        public void Shutdown()
        {
            Application.logMessageReceived -= OnLog;
        }

        private void OnLog(string message, string stackTrace, LogType type)
        {
            lock (_lock)
            {
                _entries.AddLast(new ConsoleEntry
                {
                    Timestamp = DateTime.UtcNow.ToString("o"),
                    Type = ToBridgeType(type),
                    Message = message ?? string.Empty,
                    StackTrace = stackTrace ?? string.Empty,
                });
                while (_entries.Count > Capacity)
                    _entries.RemoveFirst();
            }
        }

        public List<ConsoleEntry> Query(int maxLogs, HashSet<string> types)
        {
            var result = new List<ConsoleEntry>();
            lock (_lock)
            {
                var node = _entries.Last;
                while (node != null && result.Count < maxLogs)
                {
                    if (types.Count == 0 || types.Contains(node.Value.Type))
                        result.Add(node.Value);
                    node = node.Previous;
                }
            }
            return result;
        }

        public void Clear()
        {
            lock (_lock) { _entries.Clear(); }
        }

        private static string ToBridgeType(LogType type)
        {
            return type switch
            {
                LogType.Warning => "warning",
                LogType.Error or LogType.Exception or LogType.Assert => "error",
                _ => "log",
            };
        }
    }

    [Serializable]
    internal sealed class ConsoleEntry
    {
        public string Timestamp = string.Empty;
        public string Type = "log";
        public string Message = string.Empty;
        public string StackTrace = string.Empty;
    }
}
