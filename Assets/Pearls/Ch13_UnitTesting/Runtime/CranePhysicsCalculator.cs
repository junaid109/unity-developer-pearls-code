using System;

namespace Pearls.Core
{
    /// <summary>
    /// TEACHING TOY. Moment = mass * g * horizontal reach. There is no rated-capacity
    /// chart, dynamic factor or safety factor here. Do not use as real safety logic.
    /// Pure logic: no Unity dependencies.
    /// </summary>
    public class CranePhysicsCalculator
    {
        public struct SafetyResult { public bool IsSafe; public float LoadMoment; }

        public const float Gravity = 9.81f;
        public const float MomentLimit = 500000f; // illustrative threshold, not a standard

        /// <param name="boomLength">Metres.</param>
        /// <param name="angleDegrees">DEGREES from horizontal (0 = flat boom).</param>
        /// <param name="loadMassKg">Kilograms.</param>
        public SafetyResult CalculateSafety(float boomLength, float angleDegrees, float loadMassKg)
        {
            float angleRad = angleDegrees * (float)Math.PI / 180f;
            float horizontalDistance = (float)Math.Cos(angleRad) * boomLength;
            float loadMoment = loadMassKg * Gravity * horizontalDistance;

            return new SafetyResult
            {
                // Strict comparison: a moment exactly at the limit is NOT safe.
                IsSafe = loadMoment < MomentLimit,
                // Rounded for display only. Rounding to 2 decimals changes the value by at most
                // 0.005, so it cannot cause drift between neighbouring angles.
                LoadMoment = (float)Math.Round(loadMoment, 2)
            };
        }
    }
}