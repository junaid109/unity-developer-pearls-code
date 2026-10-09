using NUnit.Framework;
using Pearls.Core;

namespace Pearls.Tests
{
    public class CraneSafetyTests
    {
        private CranePhysicsCalculator _calc;

        [SetUp] public void SetUp() => _calc = new CranePhysicsCalculator();

        // Closed-form values: moment = m * 9.81 * L * cos(angle). With L = 10 m and
        // m = 1000 kg the flat-boom moment is exactly 98,100.
        [TestCase(0f, 98100f)]
        [TestCase(30f, 84957.09f)]
        [TestCase(45f, 69367.18f)]
        [TestCase(60f, 49050f)]
        [TestCase(90f, 0f)]
        public void LoadMoment_MatchesClosedForm(float angle, float expected)
        {
            var result = _calc.CalculateSafety(10f, angle, 1000f);
            Assert.AreEqual(expected, result.LoadMoment, 0.05f); // tight: float precision only
        }

        // The limit is 500,000 and a flat boom at 10 m gives m * 98.1.
        // 5096 kg -> 499,917.6 (safe); 5097 kg -> 500,015.7 (unsafe).
        [TestCase(5096f, true)]
        [TestCase(5097f, false)]
        public void IsSafe_FlipsAtTheThreshold(float mass, bool expectedSafe)
        {
            Assert.AreEqual(expectedSafe, _calc.CalculateSafety(10f, 0f, mass).IsSafe);
        }

        [Test]
        public void LoadMoment_NeverIncreasesAsTheBoomRaises()
        {
            float previous = float.MaxValue;
            for (float angle = 0f; angle <= 90f; angle += 5f)
            {
                float moment = _calc.CalculateSafety(10f, angle, 5000f).LoadMoment;
                Assert.LessOrEqual(moment, previous, $"moment rose at {angle} degrees");
                previous = moment;
            }
        }

        // Guards the degrees/radians mix-up: if someone feeds radians in, 45 would be
        // read as ~2578 degrees and these closed-form checks fail loudly.
        [Test]
        public void AngleIsInDegrees()
        {
            Assert.AreEqual(49050f, _calc.CalculateSafety(10f, 60f, 1000f).LoadMoment, 0.05f);
        }
    }
}