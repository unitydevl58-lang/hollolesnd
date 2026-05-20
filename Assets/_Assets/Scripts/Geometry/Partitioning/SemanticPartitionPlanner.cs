using System.Collections.Generic;

/// <summary>
/// Applies binary partition instructions inferred from symbolic prompt analysis.
/// </summary>
public sealed class SemanticPartitionPlanner
{
    private readonly GeometryGenerationSettings settings;

    public SemanticPartitionPlanner(GeometryGenerationSettings settings)
    {
        this.settings = settings;
    }

    public void Apply(IList<GeometryCommand> commands, SymbolicAnalysisResult symbolicAnalysis)
    {
        if (commands == null || symbolicAnalysis == null || !symbolicAnalysis.RequestsBinaryPartition)
            return;

        for (int index = 0; index < commands.Count; index++)
        {
            GeometryCommand command = commands[index];
            if (command == null)
                continue;

            if (command.BinaryPartition != null && command.BinaryPartition.Enabled)
                continue;

            command.BinaryPartition = new BinaryPartitionInstruction
            {
                Enabled = true,
                Axis = symbolicAnalysis.PreferredPartitionAxis,
                Gap = settings.DefaultPartitionGap
            };
        }
    }
}
