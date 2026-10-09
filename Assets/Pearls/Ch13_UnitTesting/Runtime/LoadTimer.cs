namespace Pearls.Core
{
    // --- THE SEAM ---
    public interface ITimeProvider
    {
        float CurrentTime { get; }
    }

    // --- THE LOGIC UNDER TEST ---
    public class LoadTimer
    {
        private readonly ITimeProvider _time;
        private float _overloadStartedAt = -1f;

        public LoadTimer(ITimeProvider time) => _time = time;

        public bool HasExceededGracePeriod(bool isOverloaded, float graceSeconds)
        {
            if (!isOverloaded)
            {
                _overloadStartedAt = -1f;
                return false;
            }

            if (_overloadStartedAt < 0f) _overloadStartedAt = _time.CurrentTime;
            return (_time.CurrentTime - _overloadStartedAt) >= graceSeconds;
        }
    }
}