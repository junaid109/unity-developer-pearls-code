using NUnit.Framework;
using Pearls.RacingVR;
using UnityEngine;

namespace Pearls.RacingVR.Tests
{
    /// <summary>
    /// Replays packet arrival on a fake clock through Tick(nowMs). The publisher sends one
    /// packet every 100 ms and each one arrives exactly 1000 ms "later" on the local clock,
    /// so the smoothed clock offset is exactly 1000 and the expected positions are exact.
    /// </summary>
    public class TelemetryInterpolatorTests
    {
        private GameObject _go;
        private TelemetryInterpolator _interp;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Car");
            _interp = _go.AddComponent<TelemetryInterpolator>();
            _interp.BufferDelayMs = 200.0;
            _interp.MaxExtrapolationMs = 100.0;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        private static TelemetryPacket Packet(double ts, float x) =>
            new TelemetryPacket { Timestamp = ts, Position = new Vector3(x, 0f, 0f), Rotation = Quaternion.identity };

        // Publisher time ts arrives at local time ts + 1000.
        private void Arrive(double ts, float x)
        {
            _interp.PushPacket(Packet(ts, x));
            _interp.Tick(ts + 1000.0);
        }

        private float X => _go.transform.position.x;

        [Test]
        public void RenderTimeBeforeBuffer_HoldsOldest()
        {
            Arrive(0, 0f);
            Arrive(100, 1f); // target = 1100 - 1000 - 200 = -100: older than everything
            Assert.AreEqual(0f, X, 1e-5f);
            Assert.AreEqual(1, _interp.HeldFrames);
        }

        [Test]
        public void InsideBuffer_InterpolatesBetweenBracketingPackets()
        {
            Arrive(0, 0f);
            Arrive(100, 1f);
            Arrive(200, 2f);
            int before = _interp.InterpolatedFrames; // the arrival tick at 1200 already interpolated at ts 0

            _interp.Tick(1350.0); // target 150: halfway between ts 100 and ts 200
            Assert.AreEqual(1.5f, X, 1e-4f);
            Assert.AreEqual(before + 1, _interp.InterpolatedFrames);
        }

        [Test]
        public void Starved_ExtrapolatesWithinCap_ThenHolds()
        {
            Arrive(0, 0f);
            Arrive(100, 1f);
            Arrive(200, 2f);

            _interp.Tick(1450.0); // target 250: 50 ms past newest, velocity 1 per 100 ms
            Assert.AreEqual(2.5f, X, 1e-4f);
            Assert.AreEqual(1, _interp.ExtrapolatedFrames);

            _interp.Tick(1600.0); // target 400: 200 ms past newest, beyond the 100 ms cap
            Assert.AreEqual(2f, X, 1e-4f);
        }

        [Test]
        public void OutOfOrderPackets_AreSortedBeforeInterpolation()
        {
            Arrive(0, 0f);
            // ts 200 overtakes ts 100 on the network.
            _interp.PushPacket(Packet(200, 2f));
            _interp.PushPacket(Packet(100, 1f));
            _interp.Tick(1200.0);

            // The late ts 100 packet nudges the smoothed offset from 1000 to 1002
            // (sample 1100, weight 0.02), so the target is 148 rather than 150.
            _interp.Tick(1350.0);
            Assert.AreEqual(1.48f, X, 1e-3f);
        }

        [Test]
        public void BufferIsCapped_AtMaxBufferSize()
        {
            _interp.MaxBufferSize = 4;
            for (int i = 0; i < 10; i++) Arrive(i * 100, i);
            Assert.AreEqual(10, _interp.PacketsReceived);

            // Only ts 600..900 remain, so a render time of 450 is "before the buffer".
            int heldBefore = _interp.HeldFrames;
            _interp.Tick(450 + 1000 + 200);
            Assert.AreEqual(6f, X, 1e-4f);
            Assert.AreEqual(heldBefore + 1, _interp.HeldFrames);
        }
    }
}
