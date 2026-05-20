using System.Collections.Generic;

/// <summary>
/// Alias-driven shape parser. New aliases can be registered without changing command parsing code.
/// </summary>
public sealed class AliasShapeParser : IShapeParser
{
    private readonly Dictionary<string, VoxelShape> aliases = new Dictionary<string, VoxelShape>();

    public AliasShapeParser()
    {
        RegisterAlias(VoxelShape.Cube, "cube", "box", "kup", "küp", "kupu", "küpü");
        RegisterAlias(VoxelShape.Sphere, "sphere", "ball", "kure", "küre", "kureyi", "küreyi");
        RegisterAlias(VoxelShape.Cylinder, "cylinder", "silindir", "silindiri");
    }

    public void RegisterAlias(VoxelShape shape, params string[] shapeAliases)
    {
        for (int index = 0; index < shapeAliases.Length; index++)
            aliases[TextNormalizer.NormalizeKey(shapeAliases[index])] = shape;
    }

    public bool TryParse(string rawShapeName, out VoxelShape shape)
    {
        string normalized = TextNormalizer.NormalizeKey(rawShapeName);
        return aliases.TryGetValue(normalized, out shape);
    }
}
