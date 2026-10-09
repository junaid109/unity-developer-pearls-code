using System;

namespace Pearls.FullStack
{
    public enum CircuitState { Closed, Open, HalfOpen }

    /// <summary>
    /// Wraps a failure-prone operation with circuit-breaker semantics.
    /// CLOSED   -- requests flow normally; failures are counted.
    /// OPEN     -- requests are rejected locally (no network call at all)
    ///             until the cooldown window elapses. This is what
    ///             protects a genuinely down backend from every client's
    ///             backoff-and-retry loop still adding up to real load.
    /// HALF-OPEN-- exactly one trial request is in flight; every other
    ///             caller is rejected until that trial reports back.
    ///             Success closes the circuit, failure re-opens it and
    ///             restarts the cooldown.
    /// Thread-safe: state changes happen under a lock.
    /// </summary>
    public class CircuitBreaker
    {
        private readonly object _gate = new object();
        private readonly int _failureThreshold;
        private readonly TimeSpan _cooldown;
        private int _consecutiveFailures;
        private DateTime _openedAt;
        private bool _trialInFlight;
        private CircuitState _state = CircuitState.Closed;

        public CircuitBreaker(int failureThreshold = 5, TimeSpan? cooldown = null)
        {
            _failureThreshold = failureThreshold;
            _cooldown = cooldown ?? TimeSpan.FromSeconds(30);
        }

        public bool AllowRequest()
        {
            lock (_gate)
            {
                switch (_state)
                {
                    case CircuitState.Closed:
                        return true;

                    case CircuitState.Open:
                        if (DateTime.UtcNow - _openedAt < _cooldown)
                            return false; // Still cooling down -- fail fast, no network call.
                        _state = CircuitState.HalfOpen;
                        _trialInFlight = true;
                        return true; // This caller is the single trial request.

                    default: // HalfOpen
                        if (_trialInFlight) return false; // The trial has not reported back yet.
                        _trialInFlight = true;
                        return true;
                }
            }
        }

        public void RecordSuccess()
        {
            lock (_gate)
            {
                _consecutiveFailures = 0;
                _trialInFlight = false;
                _state = CircuitState.Closed;
            }
        }

        public void RecordFailure()
        {
            lock (_gate)
            {
                _consecutiveFailures++;
                _trialInFlight = false;
                if (_state == CircuitState.HalfOpen || _consecutiveFailures >= _failureThreshold)
                {
                    _state = CircuitState.Open;
                    _openedAt = DateTime.UtcNow;
                }
            }
        }

        /// <summary>
        /// The caller gave up (cancelled) without a verdict. Free the trial slot
        /// so the circuit cannot be stuck half-open; count neither success nor failure.
        /// </summary>
        public void RecordCancelled()
        {
            lock (_gate) { _trialInFlight = false; }
        }
    }
}