using System;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Pearls.FullStack;
using UnityEngine;
using UnityEngine.TestTools;

namespace Pearls.FullStack.Tests
{
    /// <summary>Runs the client against a local TinyHttpServer on 127.0.0.1.</summary>
    public class ResilientNetworkClientTests
    {
        private GameObject _go;
        private ResilientNetworkClient _client;
        private TinyHttpServer _server;

        [SetUp]
        public void SetUp()
        {
            _server = new TinyHttpServer();
            _go = new GameObject("Net");
            _client = _go.AddComponent<ResilientNetworkClient>();
            typeof(ResilientNetworkClient).GetField("StatusEndpointUrl", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_client, _server.Url);
        }

        [TearDown]
        public void TearDown()
        {
            _server.Dispose();
            UnityEngine.Object.DestroyImmediate(_go);
        }

        /// <summary>Waits for the task; returns its exception (unwrapped) or null.</summary>
        private static IEnumerator Run(Task<TournamentStatus> task, Action<TournamentStatus, Exception> done)
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (!task.IsCompleted)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Task did not finish in time.");
                yield return null;
            }
            Exception error = task.IsFaulted ? task.Exception.InnerException : null;
            done(task.IsCompletedSuccessfully ? task.Result : null, error);
        }

        [UnityTest]
        public IEnumerator Success_ParsesPascalCaseFields()
        {
            _server.Respond = _ => new TinyHttpServer.Reply { Status = 200, Body = "{\"Status\":\"Live\",\"ActivePlayers\":42}" };

            TournamentStatus status = null;
            yield return Run(_client.GetStatusAsync(), (s, e) => status = s);

            Assert.AreEqual("Live", status.Status);
            Assert.AreEqual(42, status.ActivePlayers);
        }

        [UnityTest]
        public IEnumerator NotFound_IsNotRetried()
        {
            _server.Respond = _ => new TinyHttpServer.Reply { Status = 404, Body = "" };

            Exception error = null;
            yield return Run(_client.GetStatusAsync(), (s, e) => error = e);

            Assert.IsInstanceOf<RequestFailedException>(error);
            Assert.IsFalse(((RequestFailedException)error).IsTransient);
            Assert.AreEqual(1, _server.Hits, "A 4xx means our request is wrong; retrying cannot help.");
        }

        [UnityTest]
        public IEnumerator MalformedJson_IsNotRetried()
        {
            _server.Respond = _ => new TinyHttpServer.Reply { Status = 200, Body = "<html>gateway page</html>" };

            Exception error = null;
            yield return Run(_client.GetStatusAsync(), (s, e) => error = e);

            Assert.IsInstanceOf<RequestFailedException>(error);
            StringAssert.StartsWith("Malformed response", error.Message);
            Assert.AreEqual(1, _server.Hits);
        }

        [UnityTest]
        public IEnumerator RateLimited_WithRetryAfter_IsRetried()
        {
            _server.Respond = hit => hit == 1
                ? new TinyHttpServer.Reply { Status = 429, Body = "", ExtraHeaders = "Retry-After: 0\r\n" }
                : new TinyHttpServer.Reply { Status = 200, Body = "{\"Status\":\"Live\",\"ActivePlayers\":1}" };

            TournamentStatus status = null;
            yield return Run(_client.GetStatusAsync(), (s, e) => status = s);

            Assert.IsNotNull(status);
            Assert.AreEqual(2, _server.Hits);
        }
    }
}
