using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace Pearls.RacingVR
{
    [Serializable]
    public struct TelemetryPacket
    {
        public double Timestamp; // Publisher clock, in ms
        public Vector3 Position;
        public Quaternion Rotation;
    }

    /// <summary>
    /// Jitter buffer + interpolator for live racing telemetry. Deliberately
    /// renders BufferDelayMs behind the live edge so there is usually a
    /// window of real data to interpolate through, instead of snapping to
    /// whatever packet arrived most recently.
    ///
    /// Threading: PushPacket may be called from ANY thread (e.g. a pub/sub
    /// callback). Everything else runs on the main thread.
    /// </summary>
    public class TelemetryInterpolator : MonoBehaviour
    {
        [Header("Buffer Settings")]
        [Tooltip("Artificial delay to absorb network jitter (ms). Sensible range: 50-500.")]
        public double BufferDelayMs = 200.0;

        [Tooltip("Maximum packets to retain")]
        public int MaxBufferSize = 120; // ~2 seconds at 60Hz

        [Header("Starvation Handling")]
        [Tooltip("If the render time runs past the newest packet, extrapolate " +
                 "along the last velocity for at most this long (ms), then hold.")]
        public double MaxExtrapolationMs = 100.0;

        [Header("Clock Sync")]
        [Tooltip("0-1. Weight given to each new offset sample. Smaller = steadier, slower to follow drift.")]
        [Range(0.001f, 0.5f)]
        public float OffsetSmoothing = 0.02f;

        // Counters you can watch in the Inspector or log (see the Hands-On Challenge).
        public int PacketsReceived { get; private set; }
        public int InterpolatedFrames { get; private set; }
        public int ExtrapolatedFrames { get; private set; }
        public int HeldFrames { get; private set; } // starved past the cap, or render time before the buffer

        private readonly ConcurrentQueue<TelemetryPacket> _incoming = new ConcurrentQueue<TelemetryPacket>();
        private readonly List<TelemetryPacket> _buffer = new List<TelemetryPacket>(256);
        private double _networkTimeOffset;
        private bool _isTimeSynced;

        /// <summary>Thread-safe. Call this from the network callback.</summary>
        public void PushPacket(TelemetryPacket packet) => _incoming.Enqueue(packet);

        private void Update() => Tick(Time.unscaledTimeAsDouble * 1000.0);

        /// <summary>
        /// One frame of work at local time nowMs. Update passes Unity's clock;
        /// a test passes its own, so jitter and starvation replay exactly.
        /// </summary>
        public void Tick(double nowMs)
        {
            // Main thread only from here on: drain the queue into the sorted buffer.
            while (_incoming.TryDequeue(out TelemetryPacket packet))
            {
                Insert(packet, nowMs);
            }

            if (_buffer.Count < 2 || !_isTimeSynced) return;

            // Render BufferDelayMs behind the live edge, not "now".
            double targetTime = nowMs - _networkTimeOffset - BufferDelayMs;

            // First packet strictly AFTER targetTime (binary search, no allocation).
            int hi = UpperBound(targetTime);

            if (hi == 0)
            {
                // Render time is older than everything we hold: hold the oldest.
                Apply(_buffer[0].Position, _buffer[0].Rotation);
                HeldFrames++;
                return;
            }

            if (hi == _buffer.Count)
            {
                // Starvation: the buffer ran dry. Extrapolate briefly, then hold.
                TelemetryPacket newest = _buffer[_buffer.Count - 1];
                TelemetryPacket previous = _buffer[_buffer.Count - 2];
                double overshootMs = targetTime - newest.Timestamp;
                double spanMs = newest.Timestamp - previous.Timestamp;

                if (overshootMs <= MaxExtrapolationMs && spanMs > 0.0)
                {
                    Vector3 velocityPerMs = (newest.Position - previous.Position) / (float)spanMs;
                    Apply(newest.Position + velocityPerMs * (float)overshootMs, newest.Rotation);
                    ExtrapolatedFrames++;
                }
                else
                {
                    Apply(newest.Position, newest.Rotation);
                    HeldFrames++;
                }
                return;
            }

            TelemetryPacket pA = _buffer[hi - 1];
            TelemetryPacket pB = _buffer[hi];
            // pA.Timestamp <= targetTime < pB.Timestamp, so the span is > 0.
            float t = (float)((targetTime - pA.Timestamp) / (pB.Timestamp - pA.Timestamp));
            Apply(Vector3.Lerp(pA.Position, pB.Position, t), Quaternion.Slerp(pA.Rotation, pB.Rotation, t));
            InterpolatedFrames++;
        }

        private void Insert(TelemetryPacket packet, double nowMs)
        {
            PacketsReceived++;

            // Offset = local arrival time minus publisher time. It absorbs the
            // (unknown) one-way latency, which is fine because only changes in
            // it matter. Smoothing with an EMA stops one unlucky first packet
            // from skewing the whole session and lets the estimate follow slow
            // clock drift. It cannot separate drift from a genuine, lasting
            // change in network latency.
            double sample = nowMs - packet.Timestamp;
            if (!_isTimeSynced)
            {
                _networkTimeOffset = sample;
                _isTimeSynced = true;
            }
            else
            {
                _networkTimeOffset += (sample - _networkTimeOffset) * OffsetSmoothing;
            }

            // Sorted insert keeps out-of-order packets in timestamp order.
            _buffer.Insert(UpperBound(packet.Timestamp), packet);

            // At <=~120 small structs the shift in Insert/RemoveAt is a trivial
            // memmove and neither call allocates.
            if (_buffer.Count > MaxBufferSize) _buffer.RemoveAt(0);
        }

        /// <summary>Index of the first packet with Timestamp greater than t.</summary>
        private int UpperBound(double t)
        {
            int lo = 0, hi = _buffer.Count;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (_buffer[mid].Timestamp <= t) lo = mid + 1;
                else hi = mid;
            }
            return lo;
        }

        private void Apply(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
        }
    }
}