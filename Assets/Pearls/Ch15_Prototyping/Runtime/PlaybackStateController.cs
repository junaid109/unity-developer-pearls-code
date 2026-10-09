using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Pearls.Broadcast
{
    public enum FeedRole { Primary, PictureInPicture }

    [Serializable]
    public struct FeedHandle
    {
        public string FeedId;
        public string DisplayName; // e.g. driver name or camera label
    }

    /// <summary>
    /// Single source of truth for a multi-feed broadcast viewing session.
    /// UI components never mutate feed state directly -- they request a
    /// transition here, and this class decides what's actually allowed,
    /// so "which feed is primary" can never silently disagree with what's
    /// on screen.
    /// </summary>
    public partial class PlaybackStateController : MonoBehaviour
    {
        [Tooltip("Maximum simultaneous picture-in-picture feeds.")]
        public int MaxPictureInPicture = 3;

        public FeedHandle? Primary { get; private set; }
        public IReadOnlyList<FeedHandle> PictureInPicture => _pip;

        public event Action OnStateChanged;

        private readonly List<FeedHandle> _pip = new List<FeedHandle>();

        public void SetPrimary(FeedHandle feed)
        {
            // Promoting a feed that's currently in PIP to primary removes
            // it from the PIP list -- a feed can't be its own PIP panel.
            _pip.RemoveAll(f => f.FeedId == feed.FeedId);
            Primary = feed;
            OnStateChanged?.Invoke();
        }

        public bool TryAddPictureInPicture(FeedHandle feed)
        {
            if (Primary.HasValue && Primary.Value.FeedId == feed.FeedId)
                return false; // Already the primary feed -- reject the duplicate.

            if (_pip.Any(f => f.FeedId == feed.FeedId))
                return false; // Already showing as PIP.

            if (IsFailed(feed.FeedId))
                return false; // Don't show a feed known to be dead (see the health extension below).

            if (_pip.Count >= MaxPictureInPicture)
                return false; // At capacity -- caller decides whether to prompt a swap.

            _pip.Add(feed);
            OnStateChanged?.Invoke();
            return true;
        }

        public void RemovePictureInPicture(string feedId)
        {
            if (_pip.RemoveAll(f => f.FeedId == feedId) > 0)
                OnStateChanged?.Invoke();
        }

        /// <summary>
        /// Swap a PIP feed into the primary slot. The feed that was
        /// primary becomes a PIP panel rather than disappearing --
        /// switching what you're watching shouldn't cost you the feed
        /// you were just on.
        /// </summary>
        public void PromoteToPrimary(string pipFeedId)
        {
            int index = _pip.FindIndex(f => f.FeedId == pipFeedId);
            if (index == -1) return;

            FeedHandle promoted = _pip[index];
            _pip.RemoveAt(index);

            if (Primary.HasValue)
                _pip.Add(Primary.Value);

            Primary = promoted;
            OnStateChanged?.Invoke();
        }
    }
}