using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates a vertical cylinder using a circular XZ mask through the full Y height.
/// </summary>
public sealed class CylinderVoxelShapeGenerator : IVoxelShapeGenerator
{
    public VoxelShape Shape => VoxelShape.Cylinder;

    public void FillVoxels(int subdivision, List<Vector3Int> output)
    {
        output.Clear();

        for (int x = 0; x < subdivision; x++)
        for (int y = 0; y < subdivision; y++)
        for (int z = 0; z < subdivision; z++)
        {
            Vector3 normalizedCenter = GetNormalizedVoxelCenter(x, y, z, subdivision);
            float radialDistanceSquared = normalizedCenter.x * normalizedCenter.x + normalizedCenter.z * normalizedCenter.z;

            if (radialDistanceSquared <= 1f)
                output.Add(new Vector3Int(x, y, z));
        }
    }

    private Vector3 GetNormalizedVoxelCenter(int x, int y, int z, int subdivision)
    {
        float scale = 2f / subdivision;
        return new Vector3(
            (x + 0.5f) * scale - 1f,
            (y + 0.5f) * scale - 1f,
            (z + 0.5f) * scale - 1f);
    }
}
