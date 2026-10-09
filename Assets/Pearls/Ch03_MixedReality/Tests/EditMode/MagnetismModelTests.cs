using NUnit.Framework;
using Pearls.MixedReality;
using UnityEngine;

namespace Pearls.MixedReality.Tests
{
    public class MagnetismModelTests
    {
        private static Vector3 AtX(float x) => new Vector3(x, 0f, 0f);

        [Test]
        public void PointerOutOfRange_ReturnsRest_AndStaysInactive()
        {
            var model = new MagnetismModel();
            Vector3 result = model.Evaluate(Vector3.zero, AtX(0.5f), hasPointer: true);
            Assert.AreEqual(Vector3.zero, result);
            Assert.IsFalse(model.IsActive);
        }

        [Test]
        public void PointerHalfwayIn_PullsByStrengthTimesFalloff()
        {
            var model = new MagnetismModel(); // 0.2 m, strength 0.3, EaseInOut falloff
            Vector3 result = model.Evaluate(Vector3.zero, AtX(0.1f), hasPointer: true);

            // closeness = 1 - 0.1/0.2 = 0.5; EaseInOut(0.5) = 0.5; pull = 0.3 * 0.5 = 0.15
            Assert.AreEqual(0.015f, result.x, 1e-4f);
            Assert.IsTrue(model.IsActive);
        }

        [Test]
        public void Hysteresis_EntersAtActivation_LeavesOnlyPastReleaseMargin()
        {
            var model = new MagnetismModel(); // release at 0.2 * 1.15 = 0.23 m
            int activated = 0, deactivated = 0;
            model.Activated += _ => activated++;
            model.Deactivated += () => deactivated++;

            model.Evaluate(Vector3.zero, AtX(0.21f), true);
            Assert.IsFalse(model.IsActive, "0.21 m is outside the activation distance.");

            model.Evaluate(Vector3.zero, AtX(0.19f), true);
            Assert.IsTrue(model.IsActive);

            model.Evaluate(Vector3.zero, AtX(0.22f), true);
            Assert.IsTrue(model.IsActive, "Inside the release margin it must stay active (no flicker).");

            model.Evaluate(Vector3.zero, AtX(0.24f), true);
            Assert.IsFalse(model.IsActive);

            Assert.AreEqual(1, activated);
            Assert.AreEqual(1, deactivated);
        }

        [Test]
        public void LostTracking_RelaxesHome_AndDeactivatesOnce()
        {
            var model = new MagnetismModel();
            int deactivated = 0;
            model.Deactivated += () => deactivated++;

            model.Evaluate(Vector3.zero, AtX(0.05f), true);
            Vector3 result = model.Evaluate(Vector3.zero, AtX(0.05f), hasPointer: false);
            model.Evaluate(Vector3.zero, AtX(0.05f), hasPointer: false);

            Assert.AreEqual(Vector3.zero, result);
            Assert.AreEqual(1, deactivated);
        }

        [Test]
        public void NearestCandidate_PicksClosestInRange_OrMinusOne()
        {
            var rests = new[] { AtX(0f), AtX(0.1f), AtX(0.3f) };
            Assert.AreEqual(1, MagnetismModel.NearestCandidate(rests, AtX(0.08f), 0.2f));
            Assert.AreEqual(-1, MagnetismModel.NearestCandidate(rests, AtX(5f), 0.2f));
        }
    }
}
