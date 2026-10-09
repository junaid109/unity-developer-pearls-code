using NUnit.Framework;
using Pearls.Kiosk;

namespace Pearls.Kiosk.Tests
{
    /// <summary>Two minutes of kiosk life, replayed in microseconds on a made-up clock.</summary>
    public class IdleResetPolicyTests
    {
        private static IdleResetPolicy Policy() =>
            new IdleResetPolicy(idleResetTimeout: 120f, minSecondsBetweenResets: 5f, now: 0f);

        [Test]
        public void NotIdle_UntilTheTimeoutHasFullyPassed()
        {
            var p = Policy();
            Assert.IsFalse(p.IsIdle(now: 120f, inAttractScene: false), "Exactly 120 s is not yet 'more than' 120 s.");
            Assert.IsTrue(p.IsIdle(now: 120.1f, inAttractScene: false));
        }

        [Test]
        public void NeverIdle_InTheAttractScene()
        {
            Assert.IsFalse(Policy().IsIdle(now: 10000f, inAttractScene: true));
        }

        [Test]
        public void Interaction_RestartsTheIdleTimer()
        {
            var p = Policy();
            p.RegisterInteraction(100f);
            Assert.IsFalse(p.IsIdle(now: 200f, inAttractScene: false));
            Assert.IsTrue(p.IsIdle(now: 220.5f, inAttractScene: false));
        }

        [Test]
        public void FirstReset_IsAllowed_AndCountsAsInteraction()
        {
            var p = Policy();
            Assert.IsTrue(p.TryBeginReset(300f));
            Assert.IsFalse(p.IsIdle(now: 400f, inAttractScene: false), "The idle clock restarts at the reset.");
        }

        [Test]
        public void ResetsCloserThanTheMinimumGap_AreRefused()
        {
            var p = Policy();
            Assert.IsTrue(p.TryBeginReset(50f));
            Assert.IsFalse(p.TryBeginReset(52f), "A scene that throws on load must not cause a reset loop.");
            Assert.IsFalse(p.TryBeginReset(54.9f));
            Assert.IsTrue(p.TryBeginReset(55f));
        }
    }
}
