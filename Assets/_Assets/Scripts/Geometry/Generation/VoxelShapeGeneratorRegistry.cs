using System.Collections.Generic;

/// <summary>
/// Registry for voxel shape generators.
/// </summary>
public sealed class VoxelShapeGeneratorRegistry
{
    private readonly Dictionary<VoxelShape, IVoxelShapeGenerator> generators = new Dictionary<VoxelShape, IVoxelShapeGenerator>();

    public VoxelShapeGeneratorRegistry(IEnumerable<IVoxelShapeGenerator> initialGenerators)
    {
        foreach (IVoxelShapeGenerator generator in initialGenerators)
            Register(generator);
    }

    public void Register(IVoxelShapeGenerator generator)
    {
        if (generator == null)
            return;

        generators[generator.Shape] = generator;
    }

    public IVoxelShapeGenerator Get(VoxelShape shape)
    {
        if (generators.TryGetValue(shape, out IVoxelShapeGenerator generator))
            return generator;

        return generators[VoxelShape.Cube];
    }
}
