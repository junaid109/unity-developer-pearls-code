using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Pearls.GenerativeAI;
using UnityEngine;
using UnityEngine.TestTools;

namespace Pearls.GenerativeAI.Tests
{
    /// <summary>
    /// Runs the client against a local TinyHttpServer on 127.0.0.1, so the tests need no
    /// internet access and the "broker" can answer, fail or hang on demand.
    /// </summary>
    public class GenerativeRequestClientTests
    {
        private GameObject _go;
        private GenerativeRequestClient _client;
        private TinyHttpServer _server;

        private void Set(string field, object value) =>
            typeof(GenerativeRequestClient).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_client, value);

        private Dictionary<string, string> Cache =>
            (Dictionary<string, string>)typeof(GenerativeRequestClient)
                .GetField("_cache", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_client);

        [SetUp]
        public void SetUp()
        {
            _server = new TinyHttpServer();
            _go = new GameObject("GenAI");
            _client = _go.AddComponent<GenerativeRequestClient>();
            Set("brokerEndpointUrl", _server.Url);
            Set("timeoutSeconds", 2f);
        }

        [TearDown]
        public void TearDown()
        {
            _server.Dispose();
            UnityEngine.Object.DestroyImmediate(_go);
        }

        private static IEnumerator Await<T>(Task<T> task, Action<T> onResult, float timeout = 15f)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!task.IsCompleted)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Task did not finish in time.");
                yield return null;
            }
            if (task.IsFaulted) throw task.Exception;
            onResult(task.Result);
        }

        [UnityTest]
        public IEnumerator Success_IsReturned_ThenServedFromCache()
        {
            _server.Respond = _ => new TinyHttpServer.Reply { Status = 200, Body = "Welcome, traveller." };

            GenerationResult first = null, second = null;
            yield return Await(_client.GenerateAsync("greet", "{}"), r => first = r);
            yield return Await(_client.GenerateAsync("greet", "{}"), r => second = r);

            Assert.AreEqual("Welcome, traveller.", first.Text);
            Assert.IsFalse(first.FromCache);
            Assert.IsTrue(second.FromCache);
            Assert.AreEqual(1, _server.Hits, "The cached call must not reach the broker.");
        }

        [UnityTest]
        public IEnumerator ServerError_FallsBack()
        {
            _server.Respond = _ => new TinyHttpServer.Reply { Status = 500, Body = "nope" };

            GenerationResult result = null;
            yield return Await(_client.GenerateAsync("greet", "{}"), r => result = r);

            Assert.IsTrue(result.FromFallback);
            Assert.AreEqual("...", result.Text);
        }

        [UnityTest]
        public IEnumerator Timeout_FallsBack_AndAbortsTheRequest()
        {
            _server.Respond = _ => null; // the broker accepts the request and never answers
            Set("timeoutSeconds", 0.5f);

            GenerationResult result = null;
            float started = Time.realtimeSinceStartup;
            yield return Await(_client.GenerateAsync("greet", "{}"), r => result = r);

            Assert.IsTrue(result.FromFallback);
            Assert.Less(Time.realtimeSinceStartup - started, 3f, "The latency budget must hold.");

            // The fallback is not enough: the HTTP request itself must stop, or it keeps
            // a connection and bandwidth busy after the player has moved on.
            float deadline = Time.realtimeSinceStartup + 3f;
            while (_server.ClientsGaveUp == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(1, _server.ClientsGaveUp, "The timed-out request was not aborted.");
        }

        [UnityTest]
        public IEnumerator Retry_SucceedsOnSecondAttempt()
        {
            _server.Respond = hit => hit == 1
                ? new TinyHttpServer.Reply { Status = 503, Body = "busy" }
                : new TinyHttpServer.Reply { Status = 200, Body = "second time lucky" };

            GenerationResult result = null;
            yield return Await(_client.GenerateWithRetryAsync("greet", "{}"), r => result = r);

            Assert.AreEqual("second time lucky", result.Text);
            Assert.AreEqual(2, _server.Hits);
        }

        [UnityTest]
        public IEnumerator Retry_GivesUpAfterMaxRetriesPlusOne()
        {
            _server.Respond = _ => new TinyHttpServer.Reply { Status = 503, Body = "busy" };
            Set("maxRetries", 2);

            GenerationResult result = null;
            yield return Await(_client.GenerateWithRetryAsync("greet", "{}"), r => result = r);

            Assert.IsTrue(result.FromFallback);
            Assert.AreEqual(3, _server.Hits);
        }

        [UnityTest]
        public IEnumerator Cache_IsCappedAtMaxEntries()
        {
            _server.Respond = hit => new TinyHttpServer.Reply { Status = 200, Body = "line " + hit };
            Set("maxCacheEntries", 2);

            foreach (var key in new[] { "a", "b", "c" })
                yield return Await(_client.GenerateWithRetryAsync(key, "{}"), _ => { });

            Assert.AreEqual(2, Cache.Count);
            Assert.IsTrue(Cache.ContainsKey("c"), "The newest entry is always kept.");
        }
    }
}
