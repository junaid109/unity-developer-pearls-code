using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Pearls.FullStack
{
    // Shared Data Model (Ideally defined in a separate .NET Standard DLL).
    // Public FIELDS with PascalCase names: JsonUtility ignores properties and
    // matches names case-sensitively, so configure the server to match
    // (PropertyNamingPolicy = null) or this will deserialize to defaults.
    [Serializable]
    public class TournamentStatus { public string Status; public int ActivePlayers; }

    /// <summary>An HTTP/network failure, classified so the caller knows whether a retry can help.</summary>
    public class RequestFailedException : Exception
    {
        public bool IsTransient { get; }
        public TimeSpan? RetryAfter { get; }

        public RequestFailedException(string message, bool isTransient, TimeSpan? retryAfter = null)
            : base(message)
        {
            IsTransient = isTransient;
            RetryAfter = retryAfter;
        }
    }

    public class CircuitOpenException : Exception
    {
        public CircuitOpenException() : base("Circuit is open; request not sent.") { }
    }

    /// <summary>
    /// A resilient network client that protects the backend from spikes.
    /// ARCHITECTURE:
    /// - Capped exponential backoff: ceiling = min(cap, 1s * 2^(attempt-1)),
    ///   i.e. 1s, 2s, 4s, 8s... up to MAX_BACKOFF_SECONDS.
    /// - FULL jitter: the actual wait is random(0, ceiling), which spreads
    ///   clients across the whole window and prevents the "Thundering Herd".
    /// - Retries only transient failures: network errors, 5xx, 408 and 429
    ///   (honouring Retry-After). Other 4xx responses are never retried.
    /// - A circuit breaker checked before every attempt.
    /// - A CancellationToken so leaving the screen stops the loop.
    /// </summary>
    public class ResilientNetworkClient : MonoBehaviour
    {
        private const int MAX_ATTEMPTS = 5;
        private const float BASE_BACKOFF_SECONDS = 1f;
        private const float MAX_BACKOFF_SECONDS = 30f;

        [SerializeField] private string StatusEndpointUrl = "https://api.example.com/tournament/status";

        private readonly CircuitBreaker _breaker = new CircuitBreaker();

        public async Task<TournamentStatus> GetStatusAsync(CancellationToken ct = default)
        {
            for (int attempt = 1; attempt <= MAX_ATTEMPTS; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                // Fail fast, with no network call, while the circuit is open.
                if (!_breaker.AllowRequest()) throw new CircuitOpenException();

                try
                {
                    TournamentStatus result = await SendRequest(ct);
                    _breaker.RecordSuccess();
                    return result;
                }
                catch (OperationCanceledException)
                {
                    _breaker.RecordCancelled(); // Releases a half-open trial slot if we held it.
                    throw;
                }
                catch (RequestFailedException ex) when (!ex.IsTransient)
                {
                    // The server answered; our request was wrong (400, 401, 404...).
                    // Retrying cannot help, and it is not evidence the backend is down.
                    _breaker.RecordSuccess();
                    throw;
                }
                catch (RequestFailedException ex)
                {
                    _breaker.RecordFailure();
                    if (attempt == MAX_ATTEMPTS) throw;

                    // CAPPED EXPONENTIAL BACKOFF + FULL JITTER
                    float ceiling = Mathf.Min(MAX_BACKOFF_SECONDS,
                        BASE_BACKOFF_SECONDS * Mathf.Pow(2f, attempt - 1)); // 1, 2, 4, 8...
                    float delay = UnityEngine.Random.Range(0f, ceiling);

                    // A server that says when to come back knows better than we do, so Retry-After
                    // is the earliest we return. Jitter goes ON TOP of it: if every client waited
                    // exactly "Retry-After: 5", the whole fleet would return in the same second.
                    if (ex.RetryAfter.HasValue)
                        delay = (float)ex.RetryAfter.Value.TotalSeconds + UnityEngine.Random.Range(0f, ceiling);

                    Debug.LogWarning($"[Network] Attempt {attempt} failed ({ex.Message}). Retrying in {delay:F1}s...");
                    await Task.Delay(TimeSpan.FromSeconds(delay), ct);
                }
            }
            throw new InvalidOperationException("Unreachable: the last attempt either returns or throws.");
        }

        private async Task<TournamentStatus> SendRequest(CancellationToken ct)
        {
            using (UnityWebRequest www = UnityWebRequest.Get(StatusEndpointUrl))
            {
                www.timeout = 10; // Seconds. A hung request must not hold a retry slot forever.
                var operation = www.SendWebRequest();
                while (!operation.isDone)
                {
                    if (ct.IsCancellationRequested)
                    {
                        www.Abort();
                        ct.ThrowIfCancellationRequested();
                    }
                    await Task.Yield();
                }

                switch (www.result)
                {
                    case UnityWebRequest.Result.Success:
                        try
                        {
                            return JsonUtility.FromJson<TournamentStatus>(www.downloadHandler.text);
                        }
                        catch (ArgumentException e)
                        {
                            throw new RequestFailedException($"Malformed response: {e.Message}", isTransient: false);
                        }

                    case UnityWebRequest.Result.ConnectionError: // DNS, connect, timeout: transient.
                        throw new RequestFailedException(www.error, isTransient: true);

                    case UnityWebRequest.Result.ProtocolError: // The server replied with an HTTP error code.
                        long code = www.responseCode;
                        bool transient = code >= 500 || code == 408 || code == 429;
                        TimeSpan? retryAfter = null;
                        // Retry-After may also be an HTTP date; this sketch parses only the seconds form.
                        string header = www.GetResponseHeader("Retry-After");
                        if (code == 429 && int.TryParse(header, out int seconds) && seconds >= 0)
                            retryAfter = TimeSpan.FromSeconds(seconds);
                        throw new RequestFailedException($"HTTP {code}", transient, retryAfter);

                    default: // DataProcessingError etc.: not worth retrying.
                        throw new RequestFailedException(www.error, isTransient: false);
                }
            }
        }
    }
}