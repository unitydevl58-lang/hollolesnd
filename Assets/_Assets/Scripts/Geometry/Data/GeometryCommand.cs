using System;
using UnityEngine;

/// <summary>
/// Runtime-safe representation of one geometry request from Gemini.
/// </summary>
[Serializable]
public sealed class GeometryCommand
{
    public string Action = "create";
    public VoxelShape Shape = VoxelShape.Cube;
    public int Subdivision = 1;
    public string ColorValue = "white";
    public Vector3 PositionOffset = Vector3.zero;
    public float Scale = 1f;
    public BinaryPartitionInstruction BinaryPartition = BinaryPartitionInstruction.None();
}
