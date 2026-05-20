using System.Runtime.CompilerServices;
using UnityEngine;

namespace HoloLensApp.Interaction.Math
{
    /// <summary>
    /// Formal CSG/AABB interaction states derived from Wong-style spatial relations.
    /// </summary>
    public enum FormalInteractionState
    {
        Detachment = 0,
        Touching = 1,
        Penetration = 2,
        Coinciding = 3,
        Encapsulation = 4
    }

    /// <summary>
    /// Allocation-free mathematical utilities for AABB volume, formal interaction,
    /// and strict grid-position evaluation.
    /// </summary>
    public static class WongMathUtility
    {
        /// <summary>
        /// Calculates the positive volumetric overlap between two axis-aligned bounds.
        /// Returns 0 for detached, merely touching, or degenerate bounds.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CalculateIntersectionVolume(Bounds a, Bounds b)
        {
            GetAxisMinMax(a, out float aMinX, out float aMaxX, out float aMinY, out float aMaxY, out float aMinZ, out float aMaxZ);
            GetAxisMinMax(b, out float bMinX, out float bMaxX, out float bMinY, out float bMaxY, out float bMinZ, out float bMaxZ);

            float overlapX = Mathf.Min(aMaxX, bMaxX) - Mathf.Max(aMinX, bMinX);
            if (overlapX <= Mathf.Epsilon)
                return 0f;

            float overlapY = Mathf.Min(aMaxY, bMaxY) - Mathf.Max(aMinY, bMinY);
            if (overlapY <= Mathf.Epsilon)
                return 0f;

            float overlapZ = Mathf.Min(aMaxZ, bMaxZ) - Mathf.Max(aMinZ, bMinZ);
            if (overlapZ <= Mathf.Epsilon)
                return 0f;

            return overlapX * overlapY * overlapZ;
        }

        /// <summary>
        /// Calculates VA + VB - V(A intersect B).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CalculateUnionVolume(Bounds a, Bounds b)
        {
            float unionVolume = CalculateBoundsVolume(a) + CalculateBoundsVolume(b) - CalculateIntersectionVolume(a, b);
            return unionVolume <= Mathf.Epsilon ? 0f : unionVolume;
        }

        /// <summary>
        /// Calculates the remaining volume of main after subtractor removes their overlap.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CalculateSubtractionVolume(Bounds main, Bounds subtractor)
        {
            float remainingVolume = CalculateBoundsVolume(main) - CalculateIntersectionVolume(main, subtractor);
            return remainingVolume <= Mathf.Epsilon ? 0f : remainingVolume;
        }

        /// <summary>
        /// Detects the formal spatial relation between two bounds using volumetric overlap
        /// first, then closest-boundary distance for zero-volume contact.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FormalInteractionState DetectInteractionState(Bounds a, Bounds b, float tolerance = 0.01f)
        {
            float safeTolerance = NormalizeTolerance(tolerance);

            GetAxisMinMax(a, out float aMinX, out float aMaxX, out float aMinY, out float aMaxY, out float aMinZ, out float aMaxZ);
            GetAxisMinMax(b, out float bMinX, out float bMaxX, out float bMinY, out float bMaxY, out float bMinZ, out float bMaxZ);

            if (AxisDifferenceWithinTolerance(aMinX, bMinX, safeTolerance) &&
                AxisDifferenceWithinTolerance(aMaxX, bMaxX, safeTolerance) &&
                AxisDifferenceWithinTolerance(aMinY, bMinY, safeTolerance) &&
                AxisDifferenceWithinTolerance(aMaxY, bMaxY, safeTolerance) &&
                AxisDifferenceWithinTolerance(aMinZ, bMinZ, safeTolerance) &&
                AxisDifferenceWithinTolerance(aMaxZ, bMaxZ, safeTolerance))
            {
                return FormalInteractionState.Coinciding;
            }

            float overlapX = Mathf.Min(aMaxX, bMaxX) - Mathf.Max(aMinX, bMinX);
            float overlapY = Mathf.Min(aMaxY, bMaxY) - Mathf.Max(aMinY, bMinY);
            float overlapZ = Mathf.Min(aMaxZ, bMaxZ) - Mathf.Max(aMinZ, bMinZ);

            if (overlapX > Mathf.Epsilon && overlapY > Mathf.Epsilon && overlapZ > Mathf.Epsilon)
            {
                if (BoundsContainsBounds(
                        aMinX, aMaxX, aMinY, aMaxY, aMinZ, aMaxZ,
                        bMinX, bMaxX, bMinY, bMaxY, bMinZ, bMaxZ,
                        safeTolerance) ||
                    BoundsContainsBounds(
                        bMinX, bMaxX, bMinY, bMaxY, bMinZ, bMaxZ,
                        aMinX, aMaxX, aMinY, aMaxY, aMinZ, aMaxZ,
                        safeTolerance))
                {
                    return FormalInteractionState.Encapsulation;
                }

                return FormalInteractionState.Penetration;
            }

            float separationX = CalculateAxisSeparation(aMinX, aMaxX, bMinX, bMaxX);
            float separationY = CalculateAxisSeparation(aMinY, aMaxY, bMinY, bMaxY);
            float separationZ = CalculateAxisSeparation(aMinZ, aMaxZ, bMinZ, bMaxZ);
            float closestDistanceSqr = (separationX * separationX) + (separationY * separationY) + (separationZ * separationZ);
            float toleranceSqr = safeTolerance * safeTolerance;

            return closestDistanceSqr <= toleranceSqr
                ? FormalInteractionState.Touching
                : FormalInteractionState.Detachment;
        }

        /// <summary>
        /// Snaps a position to the nearest 3D grid node offset from gridOrigin.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 SnapToGrid(Vector3 currentPosition, float gridSize, Vector3 gridOrigin)
        {
            float safeGridSize = Mathf.Abs(gridSize);
            if (safeGridSize <= Mathf.Epsilon)
                safeGridSize = Mathf.Epsilon;

            Vector3 snappedPosition = currentPosition;
            snappedPosition.x = gridOrigin.x + (Mathf.Round((currentPosition.x - gridOrigin.x) / safeGridSize) * safeGridSize);
            snappedPosition.y = gridOrigin.y + (Mathf.Round((currentPosition.y - gridOrigin.y) / safeGridSize) * safeGridSize);
            snappedPosition.z = gridOrigin.z + (Mathf.Round((currentPosition.z - gridOrigin.z) / safeGridSize) * safeGridSize);
            return snappedPosition;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float CalculateBoundsVolume(Bounds bounds)
        {
            float sizeX = Mathf.Abs(bounds.size.x);
            if (sizeX <= Mathf.Epsilon)
                return 0f;

            float sizeY = Mathf.Abs(bounds.size.y);
            if (sizeY <= Mathf.Epsilon)
                return 0f;

            float sizeZ = Mathf.Abs(bounds.size.z);
            if (sizeZ <= Mathf.Epsilon)
                return 0f;

            return sizeX * sizeY * sizeZ;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void GetAxisMinMax(
            Bounds bounds,
            out float minX,
            out float maxX,
            out float minY,
            out float maxY,
            out float minZ,
            out float maxZ)
        {
            float centerX = bounds.center.x;
            float centerY = bounds.center.y;
            float centerZ = bounds.center.z;

            float halfSizeX = Mathf.Abs(bounds.size.x) * 0.5f;
            float halfSizeY = Mathf.Abs(bounds.size.y) * 0.5f;
            float halfSizeZ = Mathf.Abs(bounds.size.z) * 0.5f;

            minX = centerX - halfSizeX;
            maxX = centerX + halfSizeX;
            minY = centerY - halfSizeY;
            maxY = centerY + halfSizeY;
            minZ = centerZ - halfSizeZ;
            maxZ = centerZ + halfSizeZ;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float NormalizeTolerance(float tolerance)
        {
            float safeTolerance = Mathf.Abs(tolerance);
            return safeTolerance <= Mathf.Epsilon ? Mathf.Epsilon : safeTolerance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool AxisDifferenceWithinTolerance(float a, float b, float tolerance)
        {
            return Mathf.Abs(a - b) <= tolerance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float CalculateAxisSeparation(float aMin, float aMax, float bMin, float bMax)
        {
            if (aMax < bMin)
                return bMin - aMax;

            if (bMax < aMin)
                return aMin - bMax;

            return 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool BoundsContainsBounds(
            float outerMinX,
            float outerMaxX,
            float outerMinY,
            float outerMaxY,
            float outerMinZ,
            float outerMaxZ,
            float innerMinX,
            float innerMaxX,
            float innerMinY,
            float innerMaxY,
            float innerMinZ,
            float innerMaxZ,
            float tolerance)
        {
            return outerMinX <= innerMinX + tolerance &&
                   outerMaxX >= innerMaxX - tolerance &&
                   outerMinY <= innerMinY + tolerance &&
                   outerMaxY >= innerMaxY - tolerance &&
                   outerMinZ <= innerMinZ + tolerance &&
                   outerMaxZ >= innerMaxZ - tolerance;
        }
    }
}
