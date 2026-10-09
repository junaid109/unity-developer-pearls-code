using System.Collections.Generic;
using NUnit.Framework;
using Pearls.Architecture;
using UnityEngine;
using UnityEngine.TestTools;

namespace Pearls.Architecture.Tests
{
    public class EventBusTests
    {
        private readonly struct OtherEvent { }

        [Test]
        public void Publish_ReachesEverySubscriberOfThatType_Only()
        {
            var bus = new EventBus();
            var seen = new List<int>();
            bool otherFired = false;

            bus.Subscribe<PlayerDamagedEvent>(e => seen.Add(e.Amount));
            bus.Subscribe<PlayerDamagedEvent>(e => seen.Add(e.RemainingHealth));
            bus.Subscribe<OtherEvent>(_ => otherFired = true);

            bus.Publish(new PlayerDamagedEvent(10, 90));

            CollectionAssert.AreEqual(new[] { 10, 90 }, seen);
            Assert.IsFalse(otherFired);
        }

        [Test]
        public void Unsubscribe_RemovesOnlyThatHandler()
        {
            var bus = new EventBus();
            int a = 0, b = 0;
            System.Action<PlayerDamagedEvent> handlerA = _ => a++;
            System.Action<PlayerDamagedEvent> handlerB = _ => b++;
            bus.Subscribe(handlerA);
            bus.Subscribe(handlerB);

            bus.Unsubscribe(handlerA);
            bus.Publish(new PlayerDamagedEvent(1, 1));

            Assert.AreEqual(0, a);
            Assert.AreEqual(1, b);
        }

        [Test]
        public void PublishWithNoSubscribers_AndUnsubscribeUnknown_AreHarmless()
        {
            var bus = new EventBus();
            System.Action<PlayerDamagedEvent> handler = _ => { };
            Assert.DoesNotThrow(() => bus.Unsubscribe(handler));
            Assert.DoesNotThrow(() => bus.Publish(new PlayerDamagedEvent(1, 1)));

            bus.Subscribe(handler);
            bus.Unsubscribe(handler);
            Assert.DoesNotThrow(() => bus.Publish(new PlayerDamagedEvent(1, 1)));
        }
    }

    public class LegacyGameManagerTests
    {
        [Test]
        public void ConsumersSeeOnlyTheInterfaces()
        {
            var go = new GameObject("GameManager");
            try
            {
                var manager = go.AddComponent<LegacyGameManager>();
                IGameService game = manager;
                IPlayerService player = manager;

                GameState? observed = null;
                game.OnStateChanged += s => observed = s;
                LogAssert.Expect(LogType.Log, "[GameManager] Logic: Changing state to Playing");
                game.ChangeState(GameState.Playing);
                player.AddScore(5);
                player.AddScore(7);

                Assert.AreEqual(GameState.Playing, observed);
                Assert.AreEqual(GameState.Playing, game.CurrentState);
                Assert.AreEqual(12, player.PlayerScore);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
