using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client; // From the Microsoft .NET SignalR client DLLs you import yourself (see the note above)

namespace Pearls.DigitalTwins
{
    [Serializable]
    public class TwinState
    {
        public string TwinId;
        public string PropertyName;
        // A string keeps this sample simple: an `object` field is deserialized into
        // whatever boxed type the JSON library picks (often a JsonElement, not a
        // string), so a View's `as string` comparison could silently fail.
        public string Value;
        public DateTime LastUpdated;
    }

    /// <summary>
    /// A high-performance real-time data service using WebSockets (SignalR).
    /// </summary>
    public class SignalRTwinService : IDisposable
    {
        private HubConnection _connection;
        private readonly string _hubUrl;

        // Reactive Events
        public event Action<TwinState> OnPropertyUpdated;
        public event Action<bool> OnConnectionStatusChanged;

        // Smart Throttling: even with a push architecture, a misbehaving sensor
        // or a fast-changing property (a live tank-fill percentage, say) can still
        // flood the client with updates faster than the visual layer needs them.
        // We buffer the LATEST value per (twinId, property) key and flush on a fixed
        // tick, so the client only ever processes at most one update per key per tick:
        // bursty upstream traffic gets collapsed into a smooth, bounded output rate.
        private readonly Dictionary<string, TwinState> _pendingUpdates = new Dictionary<string, TwinState>();
        private readonly object _pendingLock = new object();
        private float _throttleIntervalSeconds = 0.1f; // 10Hz max UI update rate

        public SignalRTwinService(string hubUrl, float throttleIntervalSeconds = 0.1f)
        {
            _hubUrl = hubUrl;
            _throttleIntervalSeconds = throttleIntervalSeconds;
        }

        public async Task ConnectAsync(string accessToken)
        {
            _connection = new HubConnectionBuilder()
                .WithUrl(_hubUrl, options => {
                    options.AccessTokenProvider = () => Task.FromResult(accessToken);
                })
                .WithAutomaticReconnect()
                .Build();

            // Handle incoming data from the server
            _connection.On<TwinState>("ReceivePropertyUpdate", (state) =>
            {
                // Buffer instead of broadcasting immediately. A background thread
                // (SignalR callbacks don't run on Unity's main thread) writes into
                // the pending map, and a coroutine/Update loop on the main thread
                // flushes it on a fixed interval. Only the LATEST value per key
                // survives if several updates for the same twin/property arrive
                // within one throttle window.
                string key = $"{state.TwinId}::{state.PropertyName}";
                lock (_pendingLock)
                {
                    _pendingUpdates[key] = state;
                }
            });

            // NOTE: these lifecycle events are raised off Unity's main thread too.
            // Route status changes through the same buffer-and-flush idea (or a
            // main-thread dispatcher) before touching any Unity object from a subscriber.
            _connection.Reconnecting += (error) => {
                OnConnectionStatusChanged?.Invoke(false);
                return Task.CompletedTask;
            };

            _connection.Reconnected += (connectionId) => {
                OnConnectionStatusChanged?.Invoke(true);
                return Task.CompletedTask;
            };

            try
            {
                await _connection.StartAsync();
                OnConnectionStatusChanged?.Invoke(true);
                Debug.Log("[DigitalTwin] SignalR Connected.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DigitalTwin] Connection Failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Call this from a MonoBehaviour's Update/coroutine on a fixed interval
        /// (e.g. every 0.1s via InvokeRepeating, or an editor-driven tick). This is
        /// the ONLY place OnPropertyUpdated actually fires, which is what turns a
        /// potentially-unbounded stream of server pushes into a bounded, predictable
        /// UI update rate regardless of how chatty the upstream sensors are.
        /// </summary>
        public void FlushPendingUpdates()
        {
            List<TwinState> toDispatch = null;

            lock (_pendingLock)
            {
                if (_pendingUpdates.Count == 0) return;
                toDispatch = new List<TwinState>(_pendingUpdates.Values);
                _pendingUpdates.Clear();
            }

            foreach (var state in toDispatch)
                OnPropertyUpdated?.Invoke(state);
        }

        /// <summary>
        /// Example of sending data back to the twin (Command pattern)
        /// </summary>
        public async Task UpdatePropertyAsync(string twinId, string propName, object value)
        {
            if (_connection.State != HubConnectionState.Connected) return;

            try
            {
                await _connection.InvokeAsync("UpdateTwinProperty", twinId, propName, value);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DigitalTwin] Failed to update property: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _connection?.StopAsync();
            _connection?.DisposeAsync();
        }
    }
}