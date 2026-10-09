using System;
using System.Threading;
using NUnit.Framework;
using Pearls.FullStack;

namespace Pearls.FullStack.Tests
{
    public class CircuitBreakerTests
    {
        private static CircuitBreaker Breaker(int threshold = 3, int cooldownMs = 100) =>
            new CircuitBreaker(threshold, TimeSpan.FromMilliseconds(cooldownMs));

        private static void Fail(CircuitBreaker b, int times)
        {
            for (int i = 0; i < times; i++) { Assert.IsTrue(b.AllowRequest()); b.RecordFailure(); }
        }

        [Test]
        public void Closed_AllowsRequests_UntilThresholdFailures()
        {
            var b = Breaker();
            Fail(b, 2);
            Assert.IsTrue(b.AllowRequest(), "Two failures are below a threshold of three.");
            b.RecordFailure();
            Assert.IsFalse(b.AllowRequest(), "Third consecutive failure opens the circuit.");
        }

        [Test]
        public void SuccessResetsTheFailureCount()
        {
            var b = Breaker();
            Fail(b, 2);
            b.RecordSuccess();
            Fail(b, 2);
            Assert.IsTrue(b.AllowRequest());
        }

        [Test]
        public void AfterCooldown_ExactlyOneTrialIsAllowed()
        {
            var b = Breaker();
            Fail(b, 3);
            Thread.Sleep(150);

            Assert.IsTrue(b.AllowRequest(), "First caller after the cooldown is the trial.");
            Assert.IsFalse(b.AllowRequest(), "Everyone else waits for the trial's verdict.");
        }

        [Test]
        public void TrialSuccess_Closes_TrialFailure_ReopensAndRestartsCooldown()
        {
            var b = Breaker();
            Fail(b, 3);
            Thread.Sleep(150);
            Assert.IsTrue(b.AllowRequest());
            b.RecordFailure(); // the trial failed

            Assert.IsFalse(b.AllowRequest(), "Re-opened, with a fresh cooldown.");
            Thread.Sleep(150);
            Assert.IsTrue(b.AllowRequest());
            b.RecordSuccess();

            Assert.IsTrue(b.AllowRequest());
            Assert.IsTrue(b.AllowRequest(), "Closed again: no trial limit.");
        }

        [Test]
        public void CancelledTrial_FreesTheSlot_WithoutAVerdict()
        {
            var b = Breaker();
            Fail(b, 3);
            Thread.Sleep(150);
            Assert.IsTrue(b.AllowRequest());
            b.RecordCancelled();

            Assert.IsTrue(b.AllowRequest(), "A cancelled trial must not leave the circuit stuck half-open.");
        }
    }
}
