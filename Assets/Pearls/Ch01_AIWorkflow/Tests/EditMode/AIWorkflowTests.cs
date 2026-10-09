using System.Reflection;
using NUnit.Framework;
using Pearls.AI;
using UnityEngine;

namespace Pearls.AI.Tests
{
    /// <summary>A probe contract. Its namespace starts with "Pearls", so the generator must list it.</summary>
    public interface IProbeContract
    {
        int Ping(string message);
    }

    public class AgenticContextGeneratorTests
    {
        [Test]
        public void Context_ListsPearlsInterfacesFromOtherAssemblies()
        {
            string context = AgenticContextGenerator.GenerateProjectContext();

            StringAssert.StartsWith("# UNITY PROJECT CONTEXT", context);
            StringAssert.Contains("Unity Version: " + Application.unityVersion, context);
            // IProbeContract lives in this test assembly, not in Pearls.Ch01.Runtime.
            // The listing scans every loaded assembly, so it must still appear.
            StringAssert.Contains("- IProbeContract:", context);
            StringAssert.Contains("* Ping(String) -> Int32", context);
        }
    }

    public class LogToContextPipelineTests
    {
        private GameObject _go;
        private LogToContextPipeline _pipeline;
        private MethodInfo _handleLog;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("LogPipeline");
            _pipeline = _go.AddComponent<LogToContextPipeline>();
            // OnEnable does not run for a plain MonoBehaviour in Edit Mode, so drive the
            // private log handler directly instead of logging through Unity's console.
            _handleLog = typeof(LogToContextPipeline).GetMethod("HandleLog", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        private void Log(string message, string stack, LogType type) =>
            _handleLog.Invoke(_pipeline, new object[] { message, stack, type });

        [Test]
        public void ErrorRaisesContext_WithOnlyTheLastFifteenLines_AndOneStackTrace()
        {
            string captured = null;
            _pipeline.OnDiagnosticContextReady += c => captured = c;

            for (int i = 0; i < 20; i++) Log($"msg-{i:00}", "info-stack", LogType.Log);
            Assert.IsNull(captured, "Plain logs must not raise the event.");

            Log("boom", "at Foo.Bar()", LogType.Error);

            Assert.IsNotNull(captured);
            // 15 lines of context = msg-06 .. msg-19 (14 lines) plus the error itself.
            StringAssert.DoesNotContain("msg-05", captured);
            StringAssert.Contains("msg-06", captured);
            StringAssert.Contains("[Error] boom", captured);
            StringAssert.Contains("Stack: at Foo.Bar()", captured);
            StringAssert.DoesNotContain("info-stack", captured);
        }

        [Test]
        public void LongStackTrace_IsTruncated()
        {
            string captured = null;
            _pipeline.OnDiagnosticContextReady += c => captured = c;

            Log("boom", new string('x', 600), LogType.Exception);

            StringAssert.Contains("...[truncated]", captured);
            StringAssert.DoesNotContain(new string('x', 501), captured);
        }
    }
}
