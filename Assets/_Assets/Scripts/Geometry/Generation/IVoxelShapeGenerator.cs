using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates the filled voxel coordinates for one shape.
/// </summary>
public interface IVoxelShapeGenerator
{
    VoxelShape Shape { get; }
    void FillVoxels(int subdivision, List<Vector3Int> output);
}
