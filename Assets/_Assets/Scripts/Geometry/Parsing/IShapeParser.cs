/// <summary>
/// Resolves model or user-provided shape names into supported runtime shapes.
/// </summary>
public interface IShapeParser
{
    bool TryParse(string rawShapeName, out VoxelShape shape);
}
