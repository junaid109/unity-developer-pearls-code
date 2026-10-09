using System;
using System.Collections.Generic;

namespace Pearls.Broadcast
{
    public enum FeedHealth { Live, Buffering, Failed }

    /// <summary>
    /// Extends the state owner's authority to feed health, not just feed
    /// assignment. A feed that fails is demoted the same way a UI-driven
    /// transition would be -- through the single state owner, so "what's
    /// actually on screen" and "what the UI thinks is on screen" can never
    /// silently diverge.
    /// </summary>
    public partial class PlaybackStateController
    {
        private readonly Dictionary<string, FeedHealth> _health = new Dictionary<string, FeedHealth>();

        public event Action<FeedHandle> OnPrimaryFeedFailed;

        private bool IsFailed(string feedId) =>
            _health.TryGetValue(feedId, out FeedHealth h) && h == FeedHealth.Failed;

        public void ReportFeedHealth(string feedId, FeedHealth health)
        {
            _health[feedId] = health;

            if (health != FeedHealth.Failed) return;

            // A PIP feed failing is low-stakes: drop it silently and let the
            // viewer re-add it if the feed recovers.
            if (_pip.RemoveAll(f => f.FeedId == feedId) > 0)
            {
                OnStateChanged?.Invoke();
                return;
            }

            // The primary feed failing is a different story -- it can't
            // just vanish. Fall back to the best available PIP feed if one
            // exists, or surface the failure so the UI can show a clear
            // "feed unavailable" state instead of a frozen frame.
            if (Primary.HasValue && Primary.Value.FeedId == feedId)
            {
                FeedHandle failedPrimary = Primary.Value;
                if (_pip.Count > 0)
                {
                    // PromoteToPrimary would re-add the old primary as a PIP
                    // panel -- exactly wrong for a dead feed -- so promote directly.
                    Primary = _pip[0];
                    _pip.RemoveAt(0);
                    OnStateChanged?.Invoke();
                }
                else
                {
                    Primary = null;
                    OnStateChanged?.Invoke();
                }
                OnPrimaryFeedFailed?.Invoke(failedPrimary);
            }
        }
    }
}