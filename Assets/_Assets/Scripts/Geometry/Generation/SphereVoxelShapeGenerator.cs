using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates voxels whose centers fall inside a unit sphere.
/// </summary>
public sealed class SphereVoxelShapeGenerator : IVoxelShapeGenerator
{
    public VoxelShape Shape => VoxelShape.Sphere;

    public void FillVoxels(int subdivision, List<Vector3Int> output)
    {
        output.Clear();

        for (int x = 0; x < subdivision; x++)
        for (int y = 0; y < subdivision; y++)
        for (int z = 0; z < subdivision; z++)
        {
            Vector3 normalizedCenter = GetNormalizedVoxelCenter(x, y, z, subdivision);
            if (normalizedCenter.sqrMagnitude <= 1f)
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
