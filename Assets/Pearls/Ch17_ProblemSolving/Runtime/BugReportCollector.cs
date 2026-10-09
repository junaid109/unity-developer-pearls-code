// BugReportCollector.cs - thin Unity adapter over the pure classes above.
using UnityEngine;

namespace Pearls.Stuck
{
    public sealed class BugReportCollector : MonoBehaviour
    {
        [SerializeField, Min(1)] private int logLines = 50;
        [Tooltip("Written into the build by CI, e.g. the short commit hash.")]
        [SerializeField] private string gitCommit = "";
        [SerializeField] private string summary = "";
        [SerializeField, TextArea] private string expected = "";
        [SerializeField, TextArea] private string actual = "";

        private LogRingBuffer _buffer;

        // Created on first use as well as in Awake: [ContextMenu] runs in Edit Mode,
        // where Awake has not run, and a buffer made only in Awake would still be null.
        private LogRingBuffer Buffer => _buffer ??= new LogRingBuffer(Mathf.Max(1, logLines));

        private void Awake() => _ = Buffer; // create it on the main thread before any log arrives
        private void OnEnable() => Application.logMessageReceivedThreaded += OnLog;
        private void OnDisable() => Application.logMessageReceivedThreaded -= OnLog;

        private void OnLog(string message, string stackTrace, LogType type)
        {
            Buffer.Add($"[{type}] {message}");
            // For errors, keep the top stack frame: it's usually where to start looking.
            if ((type == LogType.Error || type == LogType.Exception) && !string.IsNullOrEmpty(stackTrace))
                Buffer.Add("    at " + stackTrace.Split('\n')[0]);
        }

        public EnvironmentFacts CollectFacts() => new EnvironmentFacts
        {
            UnityVersion = Application.unityVersion,
            AppVersion = Application.version,
            Platform = Application.platform.ToString(),
            ScriptingBackend = Application.isEditor ? "Mono (Editor)" : RuntimeBackend(),
            GraphicsApi = SystemInfo.graphicsDeviceType.ToString(),
            DeviceModel = SystemInfo.deviceModel,
            OperatingSystem = SystemInfo.operatingSystem,
            SystemMemoryMb = SystemInfo.systemMemorySize,
            GitCommit = string.IsNullOrEmpty(gitCommit) ? "not supplied" : gitCommit
        };

        private static string RuntimeBackend()
        {
#if ENABLE_IL2CPP
            return "IL2CPP";
#else
            return "Mono";
#endif
        }

        /// <summary>Builds the report and copies it to the clipboard for pasting into a ticket.</summary>
        [ContextMenu("Copy bug report")]
        public string CopyReport()
        {
            var draft = new BugReportDraft { Summary = summary, Expected = expected, Actual = actual };
            string markdown = BugReportFormatter.ToMarkdown(draft, CollectFacts(), Buffer.Snapshot());
            GUIUtility.systemCopyBuffer = markdown;
            return markdown;
        }
    }
}