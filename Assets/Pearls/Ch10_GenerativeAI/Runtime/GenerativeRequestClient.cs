using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Pearls.GenerativeAI
{
    [Serializable]
    public class GenerationResult
    {
        public string Text;
        public bool FromCache;
        public bool FromFallback;
    }

    /// <summary>
    /// Client-side gateway for runtime generative AI requests.
    /// Talks to a server-side broker (never a provider API directly), enforces
    /// a hard latency budget, and caches identical requests within a session.
    /// </summary>
    public class GenerativeRequestClient : MonoBehaviour
    {
        [SerializeField] private string brokerEndpointUrl = "https://api.example.com/ai/generate";
        [SerializeField] private float timeoutSeconds = 4f;
        [SerializeField] private string fallbackLine = "...";

        private readonly Dictionary<string, string> _cache = new Dictionary<string, string>();

        public async Task<GenerationResult> GenerateAsync(string promptKey, string scopedPrompt)
        {
            if (_cache.TryGetValue(promptKey, out var cached))
            {
                return new GenerationResult { Text = cached, FromCache = true };
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                string result = await SendToBroker(scopedPrompt, cts.Token);
                _cache[promptKey] = result;
                return new GenerationResult { Text = result };
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning($"[GenAI] Request '{promptKey}' exceeded {timeoutSeconds}s budget. Falling back.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GenAI] Request '{promptKey}' failed: {e.Message}. Falling back.");
            }

            return new GenerationResult { Text = fallbackLine, FromFallback = true };
        }

        private async Task<string> SendToBroker(string scopedPrompt, CancellationToken token)
        {
            // The broker owns the provider API key, moderation, and rate limiting.
            // The client only ever talks to our own backend.
            using var www = UnityWebRequest.Post(brokerEndpointUrl, scopedPrompt, "application/json");
            var operation = www.SendWebRequest();

            while (!operation.isDone)
            {
                if (token.IsCancellationRequested)
                {
                    // Stop the transfer explicitly. Disposing the request (the using above)
                    // also aborts it, but saying so here keeps the intent visible.
                    www.Abort();
                    throw new OperationCanceledException(token);
                }
                await Task.Yield();
            }

            if (www.result != UnityWebRequest.Result.Success)
                throw new Exception(www.error);

            return www.downloadHandler.text;
        }

        // Extension of GenerativeRequestClient: bounded retry with exponential backoff,
        // and a simple size cap on the session cache so a long play session doesn't
        // grow the dictionary unboundedly.
        [SerializeField] private int maxRetries = 2;
        [SerializeField] private int maxCacheEntries = 200;

        public async Task<GenerationResult> GenerateWithRetryAsync(string promptKey, string scopedPrompt)
        {
            if (_cache.TryGetValue(promptKey, out var cached))
                return new GenerationResult { Text = cached, FromCache = true };

            Exception lastError = null;

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                try
                {
                    string result = await SendToBroker(scopedPrompt, cts.Token);
                    CacheWithEviction(promptKey, result);
                    return new GenerationResult { Text = result };
                }
                catch (Exception e)
                {
                    lastError = e;
                    if (attempt < maxRetries)
                    {
                        // Exponential backoff so a transient blip doesn't turn into a retry storm
                        // against a broker that's already struggling under load.
                        int delayMs = (int)(250 * Mathf.Pow(2, attempt));
                        await Task.Delay(delayMs);
                    }
                }
            }

            Debug.LogWarning($"[GenAI] Request '{promptKey}' failed after {maxRetries + 1} attempts: {lastError?.Message}");
            return new GenerationResult { Text = fallbackLine, FromFallback = true };
        }

        private void CacheWithEviction(string key, string value)
        {
            if (_cache.Count >= maxCacheEntries && !_cache.ContainsKey(key))
            {
                // Simplest possible eviction: drop an arbitrary entry rather than let the
                // dictionary grow unbounded across a long session. A production system with
                // a real hit-rate concern would use an LRU structure instead.
                var firstKey = System.Linq.Enumerable.First(_cache.Keys);
                _cache.Remove(firstKey);
            }
            _cache[key] = value;
        }
    }
}
