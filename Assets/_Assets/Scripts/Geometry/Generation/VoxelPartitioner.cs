using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Splits voxel coordinates into two spatial halves.
/// </summary>
public sealed class VoxelPartitioner
{
    public void Split(
        IList<Vector3Int> source,
        int subdivision,
        BinaryPartitionInstruction instruction,
        List<Vector3Int> firstHalf,
        List<Vector3Int> secondHalf)
    {
        firstHalf.Clear();
        secondHalf.Clear();

        int midpoint = Mathf.Max(1, subdivision / 2);

        for (int index = 0; index < source.Count; index++)
        {
            Vector3Int coordinate = source[index];
            int axisValue = GetAxisValue(coordinate, instruction.Axis);

            if (axisValue < midpoint)
                firstHalf.Add(coordinate);
            else
                secondHalf.Add(coordinate);
        }

        if (firstHalf.Count == 0 || secondHalf.Count == 0)
            SplitByCountFallback(source, firstHalf, secondHalf);
    }

    private int GetAxisValue(Vector3Int coordinate, PartitionAxis axis)
    {
        switch (axis)
        {
            case PartitionAxis.Y:
                return coordinate.y;
            case PartitionAxis.Z:
                return coordinate.z;
            default:
                return coordinate.x;
        }
    }

    private void SplitByCountFallback(IList<Vector3Int> source, List<Vector3Int> firstHalf, List<Vector3Int> secondHalf)
    {
        firstHalf.Clear();
        secondHalf.Clear();

        int midpoint = source.Count / 2;
        for (int index = 0; index < source.Count; index++)
        {
            if (index < midpoint)
                firstHalf.Add(source[index]);
            else
                secondHalf.Add(source[index]);
        }
    }
}
