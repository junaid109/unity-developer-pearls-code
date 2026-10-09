namespace Pearls.Kiosk
{
    /// <summary>
    /// The decisions behind KioskArmor's resets, with no Unity types: when is the kiosk
    /// idle, and may a reset run right now? Times are plain seconds from any clock, so a
    /// test can drive it with made-up numbers instead of waiting two minutes.
    /// </summary>
    public sealed class IdleResetPolicy
    {
        public float IdleResetTimeout { get; set; }
        public float MinSecondsBetweenResets { get; set; }

        private float _lastInteractionTime;
        private float _lastResetTime = float.NegativeInfinity;

        public IdleResetPolicy(float idleResetTimeout, float minSecondsBetweenResets, float now)
        {
            IdleResetTimeout = idleResetTimeout;
            MinSecondsBetweenResets = minSecondsBetweenResets;
            _lastInteractionTime = now;
        }

        /// <summary>A visitor touched, clicked or pressed something.</summary>
        public void RegisterInteraction(float now) => _lastInteractionTime = now;

        /// <summary>True when nobody has interacted for longer than the timeout, outside the attract scene.</summary>
        public bool IsIdle(float now, bool inAttractScene) =>
            !inAttractScene && now - _lastInteractionTime > IdleResetTimeout;

        /// <summary>
        /// Returns true and records the reset if one may run now. Returns false when the last
        /// reset was too recent, so a scene that throws on load cannot cause a reset loop.
        /// A reset also counts as an interaction, so the idle timer starts again from it.
        /// </summary>
        public bool TryBeginReset(float now)
        {
            if (now - _lastResetTime < MinSecondsBetweenResets) return false;
            _lastResetTime = now;
            _lastInteractionTime = now;
            return true;
        }
    }
}
