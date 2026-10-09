using NUnit.Framework;
using Pearls.Core;

namespace Pearls.Tests
{
    // Hand-written fake: no dynamic proxies, so it also works on IL2CPP/AOT players.
    // The NSubstitute version of the first test lives in Optional/NSubstitute, because
    // NSubstitute does not ship with Unity.
    public class FakeTimeProvider : ITimeProvider
    {
        public float CurrentTime { get; set; }
    }

    public class LoadTimerTests
    {
        [Test]
        public void HasExceededGracePeriod_BeforeGraceElapses_ReturnsFalse()
        {
            var fakeTime = new FakeTimeProvider { CurrentTime = 0f };
            var timer = new LoadTimer(fakeTime);

            timer.HasExceededGracePeriod(isOverloaded: true, graceSeconds: 5f);
            fakeTime.CurrentTime = 2f;

            Assert.IsFalse(timer.HasExceededGracePeriod(isOverloaded: true, graceSeconds: 5f));
        }

        [Test]
        public void HasExceededGracePeriod_AfterGraceElapses_ReturnsTrue()
        {
            // Same idea with the hand-written fake.
            var fakeTime = new FakeTimeProvider { CurrentTime = 0f };
            var timer = new LoadTimer(fakeTime);

            timer.HasExceededGracePeriod(isOverloaded: true, graceSeconds: 5f);
            fakeTime.CurrentTime = 6f; // Jump straight past the grace period.

            Assert.IsTrue(timer.HasExceededGracePeriod(isOverloaded: true, graceSeconds: 5f));
        }

        [Test]
        public void ClearingTheOverload_RestartsTheGraceWindow()
        {
            var fakeTime = new FakeTimeProvider { CurrentTime = 0f };
            var timer = new LoadTimer(fakeTime);

            timer.HasExceededGracePeriod(true, 5f);
            fakeTime.CurrentTime = 4f;
            Assert.IsFalse(timer.HasExceededGracePeriod(false, 5f)); // load drops for a moment

            fakeTime.CurrentTime = 8f;  // overloaded again: the clock restarts at 8
            Assert.IsFalse(timer.HasExceededGracePeriod(true, 5f));
            fakeTime.CurrentTime = 13f;
            Assert.IsTrue(timer.HasExceededGracePeriod(true, 5f));
        }

        [Test]
        public void ExactlyAtTheGraceBoundary_CountsAsExceeded()
        {
            var fakeTime = new FakeTimeProvider { CurrentTime = 10f };
            var timer = new LoadTimer(fakeTime);
            timer.HasExceededGracePeriod(true, 5f);
            fakeTime.CurrentTime = 15f;
            Assert.IsTrue(timer.HasExceededGracePeriod(true, 5f), "The check is >=, so 5.0 s exactly trips it.");
        }
    }
}
