/// <summary>
/// Shared generation limits used by validation, layout, and mesh creation.
/// </summary>
public sealed class GeometryGenerationSettings
{
    public int MinSubdivision = 1;
    public int MinCurvedSubdivision = 8;
    public int MaxSubdivision = 10;
    public float BaseObjectSize = 0.5f;
    public float VoxelGapRatio = 0.94f;
    public float DefaultObjectGap = 0.2f;
    public float DefaultPartitionGap = 0.06f;
    public float MinScale = 0.01f;
    public float MaxScale = 10f;
}
