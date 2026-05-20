using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds Unity GameObjects from validated geometry commands.
/// </summary>
public sealed class VoxelSceneBuilder
{
    private readonly GeometryGenerationSettings settings;
    private readonly VoxelShapeGeneratorRegistry shapeRegistry;
    private readonly VoxelMeshBuilder meshBuilder;
    private readonly VoxelPartitioner partitioner;
    private readonly MaterialCache materialCache;

    private readonly List<Vector3Int> voxels = new List<Vector3Int>(1024);
    private readonly List<Vector3Int> firstHalf = new List<Vector3Int>(512);
    private readonly List<Vector3Int> secondHalf = new List<Vector3Int>(512);

    public VoxelSceneBuilder(
        GeometryGenerationSettings settings,
        VoxelShapeGeneratorRegistry shapeRegistry,
        VoxelMeshBuilder meshBuilder,
        VoxelPartitioner partitioner,
        MaterialCache materialCache)
    {
        this.settings = settings;
        this.shapeRegistry = shapeRegistry;
        this.meshBuilder = meshBuilder;
        this.partitioner = partitioner;
        this.materialCache = materialCache;
    }

    public GameObject BuildScene(IList<GeometryCommand> commands, string sceneRootName, Vector3 spawnOrigin)
    {
        GameObject root = new GameObject(sceneRootName);

        for (int index = 0; index < commands.Count; index++)
            BuildCommand(commands[index], root.transform, spawnOrigin);

        return root;
    }

    private void BuildCommand(GeometryCommand command, Transform root, Vector3 spawnOrigin)
    {
        IVoxelShapeGenerator generator = shapeRegistry.Get(command.Shape);
        generator.FillVoxels(command.Subdivision, voxels);

        GameObject group = new GameObject(BuildObjectName(command));
        group.transform.SetParent(root, worldPositionStays: false);
        group.transform.position = spawnOrigin + command.PositionOffset;

        float objectSize = settings.BaseObjectSize * command.Scale;

        if (command.BinaryPartition != null && command.BinaryPartition.Enabled)
            BuildPartitionedMeshes(command, group.transform, objectSize);
        else
            BuildSingleMesh(command, group.transform, objectSize);
    }

    private void BuildSingleMesh(GeometryCommand command, Transform parent, float objectSize)
    {
        Mesh mesh = meshBuilder.Build($"{parent.name}_Mesh", voxels, command.Subdivision, objectSize, settings.VoxelGapRatio);
        Material material = materialCache.GetMaterial(command.ColorValue);
        CreateMeshEntity(parent.name + "_Geometry", parent, mesh, material, Vector3.zero);
    }

    private void BuildPartitionedMeshes(GeometryCommand command, Transform parent, float objectSize)
    {
        partitioner.Split(voxels, command.Subdivision, command.BinaryPartition, firstHalf, secondHalf);

        Vector3 axisOffset = GetAxisVector(command.BinaryPartition.Axis) * (command.BinaryPartition.Gap * 0.5f);
        Material firstMaterial = materialCache.GetMaterial(string.IsNullOrWhiteSpace(command.BinaryPartition.FirstColor) ? command.ColorValue : command.BinaryPartition.FirstColor);
        Material secondMaterial = materialCache.GetMaterial(string.IsNullOrWhiteSpace(command.BinaryPartition.SecondColor) ? command.ColorValue : command.BinaryPartition.SecondColor);

        if (firstHalf.Count > 0)
        {
            Mesh firstMesh = meshBuilder.Build($"{parent.name}_FirstHalfMesh", firstHalf, command.Subdivision, objectSize, settings.VoxelGapRatio);
            CreateMeshEntity(parent.name + "_FirstHalf", parent, firstMesh, firstMaterial, -axisOffset);
        }

        if (secondHalf.Count > 0)
        {
            Mesh secondMesh = meshBuilder.Build($"{parent.name}_SecondHalfMesh", secondHalf, command.Subdivision, objectSize, settings.VoxelGapRatio);
            CreateMeshEntity(parent.name + "_SecondHalf", parent, secondMesh, secondMaterial, axisOffset);
        }
    }

    private GameObject CreateMeshEntity(string name, Transform parent, Mesh mesh, Material material, Vector3 localPosition)
    {
        GameObject meshObject = new GameObject(name);
        meshObject.transform.SetParent(parent, worldPositionStays: false);
        meshObject.transform.localPosition = localPosition;

        MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        MeshRenderer meshRenderer = meshObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;

        GeneratedMeshDisposer disposer = meshObject.AddComponent<GeneratedMeshDisposer>();
        disposer.Initialize(mesh);

        return meshObject;
    }

    private Vector3 GetAxisVector(PartitionAxis axis)
    {
        switch (axis)
        {
            case PartitionAxis.Y:
                return Vector3.up;
            case PartitionAxis.Z:
                return Vector3.forward;
            default:
                return Vector3.right;
        }
    }

    private string BuildObjectName(GeometryCommand command)
    {
        string colorName = string.IsNullOrWhiteSpace(command.ColorValue)
            ? "white"
            : TextNormalizer.NormalizeKey(command.ColorValue).Replace("#", "hex");

        return $"{command.Shape}_{colorName}_{command.Subdivision}x";
    }
}
