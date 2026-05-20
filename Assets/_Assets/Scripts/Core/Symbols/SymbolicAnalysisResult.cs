using System.Collections.Generic;

/// <summary>
/// Symbolic decomposition of a prompt plus high-level semantic hints.
/// ISAS 2018 hint fields carry detected academic vocabulary so GeminiConnection
/// can reinforce the prompt with the student's intent.
/// </summary>
public sealed class SymbolicAnalysisResult
{
    public readonly List<CommandSymbol> Symbols = new List<CommandSymbol>();

    // Binary partition (existing)
    public bool RequestsBinaryPartition;
    public PartitionAxis PreferredPartitionAxis = PartitionAxis.X;

    // ISAS 2018 hints — empty string means "not detected"
    /// <summary>Wong (1969) interaction keyword found in the prompt, e.g. "touching".</summary>
    public string DetectedFormInteraction = string.Empty;
    /// <summary>Ching (2014) design principle keyword found in the prompt, e.g. "harmony".</summary>
    public string DetectedDesignPrinciple = string.Empty;
    /// <summary>Ching (2014) organization schema keyword found in the prompt, e.g. "linear".</summary>
    public string DetectedOrganizationSchema = string.Empty;
}
