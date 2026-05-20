using UnityEngine;

namespace HoloLensApp.Interaction.Math
{
    /// <summary>
    /// Static utility class for handling mathematical edge cases and precision issues.
    /// Provides null-safe and division-by-zero safe mathematical operations.
    /// </summary>
    public static class MathUtilities
    {
        /// <summary>
        /// A small threshold value to prevent Divide by Zero or NaN errors when two objects
        /// perfectly overlap at the same center point.
        /// </summary>
        public const float Epsilon = 0.0001f;

        /// <summary>
        /// Checks if two floating point numbers are approximately equal,
        /// avoiding exact `==` equality checks which are prone to precision errors.
        /// </summary>
        public static bool IsApproximatelyEqual(float a, float b)
        {
            return Mathf.Abs(a - b) <= Epsilon;
        }

        /// <summary>
        /// Checks if two Vector3 positions are perfectly overlapping (tunneling/exact overlap edge case).
        /// </summary>
        public static bool AreCentersOverlapping(Vector3 centerA, Vector3 centerB)
        {
            return Vector3.SqrMagnitude(centerA - centerB) <= (Epsilon * Epsilon);
        }

        /// <summary>
        /// Calculates the volume of a given axis-aligned bounding box (AABB).
        /// Ensures non-negative volume and guards against degenerate (flat/empty) boxes.
        /// </summary>
        public static float CalculateVolume(Bounds bounds)
        {
            Vector3 size = bounds.size;
            float volume = size.x * size.y * size.z;
            
            // Prevent negative volumes or negative zeros
            return Mathf.Max(0f, volume);
        }

        /// <summary>
        /// Safely divides a by b, returning 0 if b is dangerously close to 0 to prevent Infinity or NaN.
        /// </summary>
        public static float SafeDivide(float a, float b)
        {
            if (Mathf.Abs(b) <= Epsilon)
            {
                return 0f;
            }
            return a / b;
        }
    }
}
