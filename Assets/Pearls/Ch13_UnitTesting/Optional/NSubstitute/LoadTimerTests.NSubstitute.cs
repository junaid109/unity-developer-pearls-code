using NUnit.Framework;
using NSubstitute;
using Pearls.Core;

namespace Pearls.Tests
{
    // Hand-written fake: no dynamic proxies, so it also works on IL2CPP/AOT players.
    public class FakeTimeProvider : ITimeProvider
    {
        public float CurrentTime { get; set; }
    }

    public class LoadTimerTests
    {
        [Test]
        public void HasExceededGracePeriod_BeforeGraceElapses_ReturnsFalse()
        {
            // Arrange: a fake ITimeProvider we can drive by hand.
            var fakeTime = Substitute.For<ITimeProvider>();
            fakeTime.CurrentTime.Returns(0f);
            var timer = new LoadTimer(fakeTime);

            // Act: start the overload condition, then advance "time" by 2s.
            timer.HasExceededGracePeriod(isOverloaded: true, graceSeconds: 5f);
            fakeTime.CurrentTime.Returns(2f);

            // Assert
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
    }
}
