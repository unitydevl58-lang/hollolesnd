using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates a filled voxel cube.
/// </summary>
public sealed class CubeVoxelShapeGenerator : IVoxelShapeGenerator
{
    public VoxelShape Shape => VoxelShape.Cube;

    public void FillVoxels(int subdivision, List<Vector3Int> output)
    {
        output.Clear();

        for (int x = 0; x < subdivision; x++)
        for (int y = 0; y < subdivision; y++)
        for (int z = 0; z < subdivision; z++)
            output.Add(new Vector3Int(x, y, z));
    }
}
