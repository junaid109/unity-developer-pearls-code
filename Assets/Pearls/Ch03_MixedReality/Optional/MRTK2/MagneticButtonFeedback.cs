using UnityEngine;

namespace Pearls.MixedReality
{
    /// <summary>
    /// Provides audio and haptic feedback for magnetic button interactions.
    /// ARCHITECTURE: Follows Single Responsibility Principle - this class ONLY handles feedback.
    /// </summary>
    [RequireComponent(typeof(MagneticButtonSolver))]
    [RequireComponent(typeof(AudioSource))]
    public class MagneticButtonFeedback : MonoBehaviour
    {
        [Header("Audio")]
        [Tooltip("Sound played when magnetism activates")]
        public AudioClip MagnetActivateClip;

        [Tooltip("Sound played when magnetism deactivates")]
        public AudioClip MagnetDeactivateClip;

        [Range(0f, 1f)]
        public float Volume = 0.5f;

        private AudioSource _audioSource;
        private MagneticButtonSolver _solver;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _solver = GetComponent<MagneticButtonSolver>();

            // BEST PRACTICE: Subscribe to events, not polling
            _solver.OnMagnetismActivated += OnMagnetActivated;
            _solver.OnMagnetismDeactivated += OnMagnetDeactivated;
        }

        private void OnDestroy()
        {
            // CRITICAL: Unsubscribe to prevent memory leaks
            if (_solver != null)
            {
                _solver.OnMagnetismActivated -= OnMagnetActivated;
                _solver.OnMagnetismDeactivated -= OnMagnetDeactivated;
            }
        }

        private void OnMagnetActivated(float strength)
        {
            if (MagnetActivateClip != null)
            {
                // PERFORMANCE: Use PlayOneShot (non-blocking, no GC allocation)
                _audioSource.PlayOneShot(MagnetActivateClip, Volume * strength);
            }

            // TODO: Add haptic feedback via MRTK's haptic service
            // Microsoft.MixedReality.Toolkit.Input.IMixedRealityHapticFeedback
        }

        private void OnMagnetDeactivated()
        {
            if (MagnetDeactivateClip != null)
            {
                _audioSource.PlayOneShot(MagnetDeactivateClip, Volume * 0.7f);
            }
        }
    }
}