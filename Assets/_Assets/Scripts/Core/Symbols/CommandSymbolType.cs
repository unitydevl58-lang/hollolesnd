/// <summary>
/// Semantic category assigned during prompt deconstruction.
/// ISAS 2018 categories (FormInteraction, DesignPrinciple, OrganizationSchema) align with
/// Wong (1969) and Ching (2014) as referenced by Kasap & Türkmen (ISAS 2018).
/// </summary>
public enum CommandSymbolType
{
    Unknown,
    Action,
    Quantity,
    Color,
    Shape,
    Position,
    SplitOperator,
    FormInteraction,    // Wong (1969): touching, overlapping, penetration, etc.
    DesignPrinciple,    // Ching (2014): harmony, balance, hierarchy, etc.
    OrganizationSchema  // Ching (2014): central, linear, radial, clustered, grid
}
