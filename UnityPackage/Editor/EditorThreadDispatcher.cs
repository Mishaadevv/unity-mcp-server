// Pumps background-thread bridge work onto Unity's main thread.
using System;
using System.Collections.Concurrent;
using UnityEditor;

namespace UnityMCP.Editor
{
    [InitializeOnLoad]
    internal static class EditorThreadDispatcher
    {
        private static readonly ConcurrentQueue<Action> Queue = new();

        static EditorThreadDispatcher()
        {
            EditorApplication.update += Pump;
        }

        public static void Enqueue(Action action)
        {
            Queue.Enqueue(action);
        }

        private static void Pump()
        {
            while (Queue.TryDequeue(out var action))
            {
                try
                {
                    action.Invoke();
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[UnityMCP] Dispatcher error: {ex}");
                }
            }
        }
    }
}
