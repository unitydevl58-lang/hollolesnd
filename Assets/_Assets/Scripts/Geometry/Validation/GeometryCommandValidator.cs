using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Applies safe runtime bounds to parsed geometry commands.
/// </summary>
public sealed class GeometryCommandValidator
{
    private readonly GeometryGenerationSettings settings;

    public GeometryCommandValidator(GeometryGenerationSettings settings)
    {
        this.settings = settings;
    }

    public void Normalize(IList<GeometryCommand> commands)
    {
        if (commands == null)
            return;

        for (int index = 0; index < commands.Count; index++)
            Normalize(commands[index]);
    }

    public void Normalize(GeometryCommand command)
    {
        if (command == null)
            return;

        if (string.IsNullOrWhiteSpace(command.Action))
            command.Action = "create";

        if (string.IsNullOrWhiteSpace(command.ColorValue))
            command.ColorValue = "white";

        command.Scale = Mathf.Clamp(command.Scale <= 0f ? 1f : command.Scale, settings.MinScale, settings.MaxScale);

        int minimumSubdivision = command.Shape == VoxelShape.Cube
            ? settings.MinSubdivision
            : settings.MinCurvedSubdivision;

        if (command.BinaryPartition != null && command.BinaryPartition.Enabled)
            minimumSubdivision = Mathf.Max(minimumSubdivision, 2);

        command.Subdivision = Mathf.Clamp(command.Subdivision <= 0 ? minimumSubdivision : command.Subdivision, minimumSubdivision, settings.MaxSubdivision);

        if (command.BinaryPartition == null)
            command.BinaryPartition = BinaryPartitionInstruction.None();

        command.BinaryPartition.Gap = Mathf.Max(0f, command.BinaryPartition.Gap);
    }

    public bool IsCreateAction(string action)
    {
        string normalized = TextNormalizer.NormalizeKey(action);
        return string.IsNullOrEmpty(normalized)
            || normalized == "create"
            || normalized == "build"
            || normalized == "make"
            || normalized == "spawn"
            || normalized == "olustur";
    }
}
