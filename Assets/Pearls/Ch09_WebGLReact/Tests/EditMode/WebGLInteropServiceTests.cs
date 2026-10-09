using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Pearls.Metaverse;
using UnityEngine;
using UnityEngine.TestTools;

namespace Pearls.Metaverse.Tests
{
    /// <summary>
    /// In the Editor the bridge logs "[Mock] Sent to React: TYPE" instead of calling
    /// JavaScript, so the log is a faithful record of what would reach the page.
    /// </summary>
    public class WebGLInteropServiceTests
    {
        [Serializable]
        private class Nav { public string Route = "/home"; }

        [SetUp]
        public void ResetStaticBridge()
        {
            // The service is static, so state would leak between tests. Reset it.
            var t = typeof(WebGLInteropService);
            t.GetField("_bridgeConfirmedReady", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, false);
            var queue = (ICollection)t.GetField("_pendingOutbound", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            queue.GetType().GetMethod("Clear").Invoke(queue, null);
        }

        [Test]
        public void MessagesBeforeReady_AreHeld_ThenFlushedInOrder()
        {
            WebGLInteropService.SendToReact("session", new Nav());
            WebGLInteropService.SendToReact("navigate", new Nav());
            LogAssert.NoUnexpectedReceived(); // nothing reached the page yet

            LogAssert.Expect(LogType.Log, "[Mock] Sent to React: session");
            LogAssert.Expect(LogType.Log, "[Mock] Sent to React: navigate");
            WebGLInteropService.ConfirmBridgeReady();
        }

        [Test]
        public void MessagesAfterReady_GoStraightThrough()
        {
            WebGLInteropService.ConfirmBridgeReady();

            LogAssert.Expect(LogType.Log, "[Mock] Sent to React: analytics");
            WebGLInteropService.SendToReact("analytics", new Nav());
        }

        [Test]
        public void EditorFallback_ReturnsMockValue()
        {
            Assert.AreEqual("MockValue", WebGLInteropService.GetValueFromBrowser("token"));
        }
    }
}
