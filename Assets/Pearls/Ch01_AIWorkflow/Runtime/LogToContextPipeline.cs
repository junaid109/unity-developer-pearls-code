using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Pearls.AI
{
    /// <summary>
    /// Captures a rolling window of Unity console output and formats it
    /// as AI-ready context when an error occurs. Attach once, near the
    /// top of your bootstrap scene.
    /// </summary>
    public class LogToContextPipeline : MonoBehaviour
    {
        [SerializeField] private int _bufferSize = 200;
        [SerializeField] private int _linesOfContextOnError = 15;

        private readonly List<LogEntry> _buffer = new();
        // Stopwatch rather than Time.realtimeSinceStartup: it is safe to read from
        // any thread, so the collector keeps working if you later switch to
        // Application.logMessageReceivedThreaded.
        private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
        public event Action<string> OnDiagnosticContextReady;

        private readonly struct LogEntry
        {
            public readonly string Message;
            public readonly string StackTrace;
            public readonly LogType Type;
            public readonly float TimeSincePlay;

            public LogEntry(string message, string stackTrace, LogType type, float time)
            {
                Message = message;
                StackTrace = stackTrace;
                Type = type;
                TimeSincePlay = time;
            }
        }

        private void OnEnable() => Application.logMessageReceived += HandleLog;
        private void OnDisable() => Application.logMessageReceived -= HandleLog;

        private void HandleLog(string message, string stackTrace, LogType type)
        {
            _buffer.Add(new LogEntry(message, stackTrace, type, (float)_clock.Elapsed.TotalSeconds));

            // Keep a fixed-size ring buffer; drop the oldest entry once full.
            if (_buffer.Count > _bufferSize) _buffer.RemoveAt(0);

            if (type == LogType.Error || type == LogType.Exception)
            {
                OnDiagnosticContextReady?.Invoke(BuildDiagnosticContext());
            }
        }

        /// <summary>
        /// Formats the most recent error plus the preceding log lines
        /// as a single Markdown block, pasteable directly into an
        /// AI agent's context, or served as an MCP resource on demand.
        /// </summary>
        public string BuildDiagnosticContext()
        {
            var sb = new StringBuilder();
            var recent = _buffer.Skip(Math.Max(0, _buffer.Count - _linesOfContextOnError));

            sb.AppendLine("# DIAGNOSTIC CONTEXT");
            sb.AppendLine($"Unity Version: {Application.unityVersion}");
            sb.AppendLine($"Captured at: {_clock.Elapsed.TotalSeconds:F2}s into session\n");
            sb.AppendLine("## Recent Log Sequence (oldest first)");

            foreach (var entry in recent)
            {
                sb.AppendLine($"[{entry.TimeSincePlay:F2}s][{entry.Type}] {entry.Message}");
                if (entry.Type == LogType.Error || entry.Type == LogType.Exception)
                {
                    // Only include the stack trace for the actual failure:
                    // including it for every line would drown the signal.
                    sb.AppendLine($"  Stack: {Truncate(entry.StackTrace, 500)}");
                }
            }

            return sb.ToString();
        }

        private static string Truncate(string s, int max) =>
            string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max) + "...[truncated]";
    }
}