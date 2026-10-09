using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pearls.MixedReality
{
    /// <summary>
    /// Platform-neutral magnetism logic. Feed it a rest position and a pointer position
    /// (a fingertip on Quest or HoloLens, a pinch location on visionOS) and it returns where
    /// the target should sit. No MRTK, XRI or PolySpatial types, so it ports as-is and
    /// can be unit-tested in Edit Mode.
    /// </summary>
    [Serializable]
    public sealed class MagnetismModel
    {
        [Tooltip("Distance (m) at which attraction starts.")]
        public float ActivationDistance = 0.2f;

        [Tooltip("Extra distance (fraction of ActivationDistance) needed before attraction stops. Stops flicker at the edge.")]
        [Range(0f, 0.5f)]
        public float ReleaseMargin = 0.15f;

        [Tooltip("Maximum fraction of the gap the target may close (0 = none).")]
        [Range(0f, 0.5f)]
        public float Strength = 0.3f;

        [Tooltip("X = closeness (0 far, 1 touching), Y = attraction multiplier.")]
        public AnimationCurve Falloff = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        /// <summary>True while the target is being attracted.</summary>
        public bool IsActive { get; private set; }

        /// <summary>Raised once on entering attraction, with the strength at that moment.</summary>
        public event Action<float> Activated;

        /// <summary>Raised once on leaving attraction.</summary>
        public event Action Deactivated;

        /// <summary>
        /// Returns the position the target should move toward this frame.
        /// Call with hasPointer = false when tracking is lost, so the target relaxes home.
        /// </summary>
        public Vector3 Evaluate(Vector3 restPosition, Vector3 pointerPosition, bool hasPointer)
        {
            if (!hasPointer || ActivationDistance <= 0f)
            {
                SetActive(false, 0f);
                return restPosition;
            }

            float distance = Vector3.Distance(restPosition, pointerPosition);
            float releaseDistance = ActivationDistance * (1f + ReleaseMargin);

            // Hysteresis: enter at ActivationDistance, leave only beyond releaseDistance.
            bool shouldBeActive = IsActive ? distance < releaseDistance : distance < ActivationDistance;
            if (!shouldBeActive)
            {
                SetActive(false, 0f);
                return restPosition;
            }

            float closeness = 1f - Mathf.Clamp01(distance / ActivationDistance);
            float pull = Strength * Mathf.Clamp01(Falloff.Evaluate(closeness));
            SetActive(true, pull);
            return Vector3.Lerp(restPosition, pointerPosition, pull);
        }

        /// <summary>
        /// Picks the single target that should respond in a dense group: the nearest one
        /// within activation range. Returns -1 if none qualifies. Allocation-free.
        /// </summary>
        public static int NearestCandidate(IReadOnlyList<Vector3> restPositions, Vector3 pointerPosition, float activationDistance)
        {
            int best = -1;
            float bestSqr = activationDistance * activationDistance;
            for (int i = 0; i < restPositions.Count; i++)
            {
                float sqr = (restPositions[i] - pointerPosition).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                }
            }
            return best;
        }

        private void SetActive(bool active, float strength)
        {
            if (active == IsActive)
            {
                return;
            }

            IsActive = active;
            if (active)
            {
                Activated?.Invoke(strength);
            }
            else
            {
                Deactivated?.Invoke();
            }
        }
    }
}