using Microsoft.MixedReality.Toolkit.Utilities.Solvers;
using UnityEngine;
using System;

namespace Pearls.MixedReality
{
    /// <summary>
    /// A custom solver that creates a "magnetic attraction" effect for UI elements,
    /// improving target acquisition in industrial MR scenarios where precision is limited
    /// (gloves, poor lighting, hand-tracking noise).
    /// 
    /// ARCHITECTURE NOTES:
    /// - Inherits from MRTK's Solver base class for consistency with the framework
    /// - Uses configurable curves for non-linear attraction (feels more natural)
    /// - Implements IDisposable pattern for proper cleanup
    /// - Exposes events for analytics and audio/haptic feedback integration
    /// </summary>
    public class MagneticButtonSolver : Solver, IDisposable
    {
        #region Configuration

        [Header("Magnetism Settings")]
        [Tooltip("Distance threshold to activate magnetic effect (in meters)")]
        [Range(0.05f, 0.5f)]
        public float ActivationDistance = 0.2f;
        
        [Tooltip("Maximum displacement of button towards hand (0 = none, 1 = full distance)")]
        [Range(0f, 0.5f)]
        public float MagnetismStrength = 0.3f;

        [Header("Performance")]
        [Tooltip("Update frequency (Hz). Lower = better performance, less responsive.")]
        [Range(30f, 90f)]
        public float UpdateFrequency = 60f;

        [Header("Feel & Polish")]
        [Tooltip("Curve defining attraction falloff. X=normalized distance, Y=attraction multiplier")]
        public AnimationCurve AttractionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Responsiveness. Smoothing time is 1/SmoothTime seconds, so higher = snappier, lower = smoother but laggier")]
        [Range(1f, 20f)]
        public float SmoothTime = 5f;

        #endregion

        #region Events

        /// <summary>
        /// Fired when magnetism activates. Use for audio/haptic feedback.
        /// </summary>
        public event Action<float> OnMagnetismActivated;

        /// <summary>
        /// Fired when magnetism deactivates.
        /// </summary>
        public event Action OnMagnetismDeactivated;

        #endregion

        #region Private State

        private Vector3 _velocity = Vector3.zero; // For SmoothDamp
        private bool _wasMagnetized = false;
        private float _lastUpdateTime = 0f;
        private Vector3 _cachedButtonRestPos;
        private Transform _cachedParent;

        #endregion

        #region Unity Lifecycle

        protected override void Start()
        {
            base.Start();
            
            // Cache the parent transform to avoid repeated GetComponent calls
            _cachedParent = transform.parent;
            
            if (_cachedParent == null)
            {
                Debug.LogError($"[MagneticButtonSolver] {gameObject.name} has no parent! " +
                              "Magnetism requires a parent anchor for rest position.", this);
            }

            // Validate configuration
            ValidateConfiguration();
        }

        #endregion

        #region Solver Override

        public override void SolverUpdate()
        {
            // PERFORMANCE: Throttle updates to configured frequency
            if (Time.time - _lastUpdateTime < (1f / UpdateFrequency))
            {
                return;
            }
            _lastUpdateTime = Time.time;

            // SAFETY: Null checks (critical in production)
            if (SolverHandler == null || SolverHandler.TransformTarget == null || _cachedParent == null)
            {
                return;
            }

            // Get current positions
            Vector3 handPos = SolverHandler.TransformTarget.position;
            _cachedButtonRestPos = _cachedParent.position;

            float distance = Vector3.Distance(handPos, _cachedButtonRestPos);
            float normalizedDistance = Mathf.Clamp01(distance / ActivationDistance);

            if (distance < ActivationDistance)
            {
                // MAGNETISM ACTIVE
                
                // Use animation curve for non-linear attraction (feels more natural)
                float curveValue = AttractionCurve.Evaluate(1f - normalizedDistance);
                float effectiveStrength = MagnetismStrength * curveValue;

                // Calculate target position with strength falloff
                Vector3 targetPos = Vector3.Lerp(_cachedButtonRestPos, handPos, effectiveStrength);
                
                // SMOOTHING: Use SmoothDamp for organic movement (not instant snap)
                GoalPosition = Vector3.SmoothDamp(
                    transform.position, 
                    targetPos, 
                    ref _velocity, 
                    1f / SmoothTime,
                    Mathf.Infinity,
                    Time.deltaTime
                );

                // Maintain original rotation (buttons shouldn't tilt)
                GoalRotation = _cachedParent.rotation;

                // EVENTS: Fire activation event (for audio/haptics)
                if (!_wasMagnetized)
                {
                    _wasMagnetized = true;
                    OnMagnetismActivated?.Invoke(effectiveStrength);
                }
            }
            else
            {
                // MAGNETISM INACTIVE - Return to rest
                
                GoalPosition = Vector3.SmoothDamp(
                    transform.position,
                    _cachedButtonRestPos,
                    ref _velocity,
                    1f / SmoothTime,
                    Mathf.Infinity,
                    Time.deltaTime
                );
                GoalRotation = _cachedParent.rotation;

                // EVENTS: Fire deactivation event
                if (_wasMagnetized)
                {
                    _wasMagnetized = false;
                    OnMagnetismDeactivated?.Invoke();
                }
            }

            // Apply the calculated goal to the transform
            UpdateWorkingPositionToGoal();
            UpdateWorkingRotationToGoal();
        }

        #endregion

        #region Validation & Debugging

        private void ValidateConfiguration()
        {
            // BEST PRACTICE: Validate configuration at runtime to catch designer errors
            
            if (ActivationDistance < 0.05f)
            {
                Debug.LogWarning($"[MagneticButtonSolver] ActivationDistance is very small ({ActivationDistance}m). " +
                                "Users may struggle to trigger magnetism.", this);
            }

            if (MagnetismStrength > 0.4f)
            {
                Debug.LogWarning($"[MagneticButtonSolver] MagnetismStrength is high ({MagnetismStrength}). " +
                                "Button may move too aggressively.", this);
            }

            if (UpdateFrequency < 30f)
            {
                Debug.LogWarning($"[MagneticButtonSolver] UpdateFrequency is low ({UpdateFrequency}Hz). " +
                                "Magnetism may feel unresponsive.", this);
            }
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            // BEST PRACTICE: Clean up event subscriptions to prevent memory leaks
            OnMagnetismActivated = null;
            OnMagnetismDeactivated = null;
        }

        private void OnDestroy()
        {
            Dispose();
        }

        #endregion

        #region Editor Helpers

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // DEBUGGING: Visualize activation radius in Scene view
            if (_cachedParent != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(_cachedParent.position, ActivationDistance);
            }
        }
#endif

        #endregion
    }
}