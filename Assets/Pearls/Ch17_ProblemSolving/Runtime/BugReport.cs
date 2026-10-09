// BugReport.cs - pure C#, no Unity dependency, so it is testable anywhere.
using System;
using System.Collections.Generic;
using System.Text;

namespace Pearls.Stuck
{
    /// <summary>Where the bug happened. Plain data, filled in by an adapter.</summary>
    public sealed class EnvironmentFacts
    {
        public string UnityVersion = "unknown", AppVersion = "unknown", Platform = "unknown";
        public string ScriptingBackend = "unknown", GraphicsApi = "unknown";
        public string DeviceModel = "unknown", OperatingSystem = "unknown";
        public string GitCommit = "not supplied";
        public int SystemMemoryMb;
    }

    /// <summary>The human half of the report. The tool can't know these, so it asks.</summary>
    public sealed class BugReportDraft
    {
        public string Summary = "", Expected = "", Actual = "", Hypothesis = "", Ask = "";
        public List<string> Steps = new List<string>();
        public List<string> Tried = new List<string>();
    }

    /// <summary>Fixed-size, thread-safe buffer of the most recent log lines.</summary>
    public sealed class LogRingBuffer
    {
        private readonly string[] _lines;
        private readonly object _gate = new object();
        private int _next, _count;

        public LogRingBuffer(int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            _lines = new string[capacity];
        }

        public void Add(string line)
        {
            lock (_gate)
            {
                _lines[_next] = line ?? "";
                _next = (_next + 1) % _lines.Length;
                if (_count < _lines.Length) _count++;
            }
        }

        /// <summary>Returns a copy, oldest line first.</summary>
        public List<string> Snapshot()
        {
            lock (_gate)
            {
                var result = new List<string>(_count);
                int start = (_next - _count + _lines.Length) % _lines.Length;
                for (int i = 0; i < _count; i++)
                    result.Add(_lines[(start + i) % _lines.Length]);
                return result;
            }
        }
    }

    /// <summary>Draft + facts + logs in, Markdown out. Pure and deterministic.</summary>
    public static class BugReportFormatter
    {
        private static readonly string Fence = new string('`', 3);

        public static string ToMarkdown(BugReportDraft d, EnvironmentFacts env, IList<string> logs)
        {
            if (d == null) throw new ArgumentNullException(nameof(d));
            if (env == null) throw new ArgumentNullException(nameof(env));
            logs = logs ?? new List<string>();

            var sb = new StringBuilder();
            sb.AppendLine("## " + OrTodo(d.Summary));
            Section(sb, "Expected", d.Expected);
            Section(sb, "Actual", d.Actual);
            Bullets(sb, "Steps to reproduce", d.Steps, numbered: true);
            Bullets(sb, "What I've tried", d.Tried, numbered: false);
            Section(sb, "What I think it is", d.Hypothesis);
            Section(sb, "What I need from you", d.Ask);

            sb.AppendLine().AppendLine("### Environment").AppendLine();
            sb.AppendLine($"- Unity {env.UnityVersion}, app {env.AppVersion}, commit {env.GitCommit}");
            sb.AppendLine($"- Platform {env.Platform}, backend {env.ScriptingBackend}, graphics {env.GraphicsApi}");
            sb.AppendLine($"- Device {env.DeviceModel}, {env.OperatingSystem}, {env.SystemMemoryMb} MB RAM");

            sb.AppendLine().AppendLine($"### Last {logs.Count} log lines").AppendLine();
            sb.AppendLine(Fence);
            foreach (var line in logs) sb.AppendLine(line);
            sb.AppendLine(Fence);
            return sb.ToString();
        }

        private static string OrTodo(string s) =>
            string.IsNullOrWhiteSpace(s) ? "_TODO: fill in_" : s.Trim();

        private static void Section(StringBuilder sb, string title, string body)
        {
            sb.AppendLine().AppendLine("### " + title).AppendLine().AppendLine(OrTodo(body));
        }

        private static void Bullets(StringBuilder sb, string title, IList<string> items, bool numbered)
        {
            sb.AppendLine().AppendLine("### " + title).AppendLine(); // blank line before a list
            if (items == null || items.Count == 0) { sb.AppendLine("- _TODO: fill in_"); return; }
            for (int i = 0; i < items.Count; i++)
                sb.AppendLine((numbered ? (i + 1) + ". " : "- ") + items[i]);
        }
    }
}