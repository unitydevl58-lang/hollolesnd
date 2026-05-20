using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds a single procedural mesh from many voxel coordinates to avoid per-voxel GameObjects.
/// </summary>
public sealed class VoxelMeshBuilder
{
    private readonly List<Vector3> vertices = new List<Vector3>(24576);
    private readonly List<Vector3> normals = new List<Vector3>(24576);
    private readonly List<int> triangles = new List<int>(36864);

    public Mesh Build(string meshName, IList<Vector3Int> voxels, int subdivision, float objectSize, float voxelGapRatio)
    {
        vertices.Clear();
        normals.Clear();
        triangles.Clear();

        float voxelSize = objectSize / subdivision;
        float visibleVoxelSize = voxelSize * voxelGapRatio;

        for (int index = 0; index < voxels.Count; index++)
        {
            Vector3Int coordinate = voxels[index];
            Vector3 center = GetCenteredVoxelPosition(coordinate.x, coordinate.y, coordinate.z, subdivision, voxelSize);
            AddCube(center, visibleVoxelSize);
        }

        Mesh mesh = new Mesh { name = meshName };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();

        return mesh;
    }

    private Vector3 GetCenteredVoxelPosition(int x, int y, int z, int subdivision, float voxelSize)
    {
        float firstCenter = -((subdivision - 1) * voxelSize) * 0.5f;
        return new Vector3(
            firstCenter + x * voxelSize,
            firstCenter + y * voxelSize,
            firstCenter + z * voxelSize);
    }

    private void AddCube(Vector3 center, float size)
    {
        float half = size * 0.5f;

        Vector3 p0 = center + new Vector3(-half, -half, -half);
        Vector3 p1 = center + new Vector3(half, -half, -half);
        Vector3 p2 = center + new Vector3(half, half, -half);
        Vector3 p3 = center + new Vector3(-half, half, -half);
        Vector3 p4 = center + new Vector3(-half, -half, half);
        Vector3 p5 = center + new Vector3(half, -half, half);
        Vector3 p6 = center + new Vector3(half, half, half);
        Vector3 p7 = center + new Vector3(-half, half, half);

        AddFace(p0, p3, p2, p1, Vector3.back);
        AddFace(p4, p5, p6, p7, Vector3.forward);
        AddFace(p0, p4, p7, p3, Vector3.left);
        AddFace(p1, p2, p6, p5, Vector3.right);
        AddFace(p3, p7, p6, p2, Vector3.up);
        AddFace(p0, p1, p5, p4, Vector3.down);
    }

    private void AddFace(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
    {
        int startIndex = vertices.Count;

        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        vertices.Add(d);

        normals.Add(normal);
        normals.Add(normal);
        normals.Add(normal);
        normals.Add(normal);

        triangles.Add(startIndex);
        triangles.Add(startIndex + 1);
        triangles.Add(startIndex + 2);
        triangles.Add(startIndex);
        triangles.Add(startIndex + 2);
        triangles.Add(startIndex + 3);
    }
}
