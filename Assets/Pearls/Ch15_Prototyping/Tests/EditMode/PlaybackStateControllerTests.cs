using System.Linq;
using NUnit.Framework;
using Pearls.Broadcast;
using UnityEngine;

namespace Pearls.Broadcast.Tests
{
    public class PlaybackStateControllerTests
    {
        private GameObject _go;
        private PlaybackStateController _c;
        private int _changes;

        private static FeedHandle Feed(string id) => new FeedHandle { FeedId = id, DisplayName = id.ToUpperInvariant() };
        private string[] PipIds => _c.PictureInPicture.Select(f => f.FeedId).ToArray();

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Playback");
            _c = _go.AddComponent<PlaybackStateController>();
            _changes = 0;
            _c.OnStateChanged += () => _changes++;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        [Test]
        public void SetPrimary_RemovesThatFeedFromPip()
        {
            _c.TryAddPictureInPicture(Feed("onboard"));
            _c.SetPrimary(Feed("onboard"));

            Assert.AreEqual("onboard", _c.Primary.Value.FeedId);
            Assert.IsEmpty(_c.PictureInPicture);
        }

        [Test]
        public void TryAddPip_RejectsPrimaryDuplicatesAndOverCapacity()
        {
            _c.MaxPictureInPicture = 2;
            _c.SetPrimary(Feed("world"));

            Assert.IsFalse(_c.TryAddPictureInPicture(Feed("world")), "Primary cannot also be PIP.");
            Assert.IsTrue(_c.TryAddPictureInPicture(Feed("a")));
            Assert.IsFalse(_c.TryAddPictureInPicture(Feed("a")), "No duplicate PIP panels.");
            Assert.IsTrue(_c.TryAddPictureInPicture(Feed("b")));
            Assert.IsFalse(_c.TryAddPictureInPicture(Feed("c")), "At capacity.");
            CollectionAssert.AreEqual(new[] { "a", "b" }, PipIds);
        }

        [Test]
        public void PromoteToPrimary_SwapsAndKeepsTheOldPrimaryAsPip()
        {
            _c.SetPrimary(Feed("world"));
            _c.TryAddPictureInPicture(Feed("onboard"));
            _changes = 0;

            _c.PromoteToPrimary("onboard");

            Assert.AreEqual("onboard", _c.Primary.Value.FeedId);
            CollectionAssert.AreEqual(new[] { "world" }, PipIds);
            Assert.AreEqual(1, _changes, "One transition, one notification.");
        }

        [Test]
        public void PromoteUnknownFeed_DoesNothing()
        {
            _c.SetPrimary(Feed("world"));
            _changes = 0;
            _c.PromoteToPrimary("ghost");
            Assert.AreEqual("world", _c.Primary.Value.FeedId);
            Assert.AreEqual(0, _changes);
        }

        [Test]
        public void FailedPipFeed_IsDropped_AndCannotBeReAdded()
        {
            _c.TryAddPictureInPicture(Feed("a"));
            _c.ReportFeedHealth("a", FeedHealth.Failed);

            Assert.IsEmpty(_c.PictureInPicture);
            Assert.IsFalse(_c.TryAddPictureInPicture(Feed("a")));

            _c.ReportFeedHealth("a", FeedHealth.Live); // recovered
            Assert.IsTrue(_c.TryAddPictureInPicture(Feed("a")));
        }

        [Test]
        public void FailedPrimary_PromotesFirstPip_AndDoesNotKeepTheDeadFeed()
        {
            FeedHandle? failed = null;
            _c.OnPrimaryFeedFailed += f => failed = f;
            _c.SetPrimary(Feed("world"));
            _c.TryAddPictureInPicture(Feed("a"));
            _c.TryAddPictureInPicture(Feed("b"));

            _c.ReportFeedHealth("world", FeedHealth.Failed);

            Assert.AreEqual("a", _c.Primary.Value.FeedId);
            CollectionAssert.AreEqual(new[] { "b" }, PipIds, "The dead feed must not come back as a PIP panel.");
            Assert.AreEqual("world", failed.Value.FeedId);
        }

        [Test]
        public void FailedPrimary_WithNoPip_LeavesNoPrimary()
        {
            _c.SetPrimary(Feed("world"));
            _c.ReportFeedHealth("world", FeedHealth.Failed);
            Assert.IsFalse(_c.Primary.HasValue);
        }

        [Test]
        public void BufferingIsNotFailure()
        {
            _c.TryAddPictureInPicture(Feed("a"));
            _c.ReportFeedHealth("a", FeedHealth.Buffering);
            CollectionAssert.AreEqual(new[] { "a" }, PipIds);
        }
    }
}
