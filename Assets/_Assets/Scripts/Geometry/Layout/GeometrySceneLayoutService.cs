using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps generated objects readable when Gemini returns missing or overlapping offsets.
/// </summary>
public sealed class GeometrySceneLayoutService
{
    private const float PositionEpsilon = 0.0001f;
    private readonly GeometryGenerationSettings settings;

    public GeometrySceneLayoutService(GeometryGenerationSettings settings)
    {
        this.settings = settings;
    }

    public void Apply(IList<GeometryCommand> commands)
    {
        if (commands == null || commands.Count <= 1)
            return;

        if (!HasMeaningfulPositions(commands))
        {
            ApplyDefaultLineLayout(commands);
            return;
        }

        EnforceMinimumHorizontalSpacing(commands);
    }

    private bool HasMeaningfulPositions(IList<GeometryCommand> commands)
    {
        for (int index = 0; index < commands.Count; index++)
        {
            GeometryCommand command = commands[index];
            if (command != null && command.PositionOffset.sqrMagnitude > PositionEpsilon)
                return true;
        }

        return false;
    }

    private void ApplyDefaultLineLayout(IList<GeometryCommand> commands)
    {
        List<GeometryCommand> layoutOrder = BuildDefaultLayoutOrder(commands);
        float spacing = GetMinimumCenterSpacing(layoutOrder);
        float startX = -spacing * (layoutOrder.Count - 1) * 0.5f;

        for (int index = 0; index < layoutOrder.Count; index++)
        {
            GeometryCommand command = layoutOrder[index];
            command.PositionOffset = new Vector3(startX + index * spacing, command.PositionOffset.y, command.PositionOffset.z);
        }
    }

    private List<GeometryCommand> BuildDefaultLayoutOrder(IList<GeometryCommand> commands)
    {
        if (commands.Count != 3)
            return new List<GeometryCommand>(commands);

        GeometryCommand middleCommand = null;
        List<GeometryCommand> sideCommands = new List<GeometryCommand>();

        for (int index = 0; index < commands.Count; index++)
        {
            GeometryCommand command = commands[index];
            if (command != null && command.Shape != VoxelShape.Cube && middleCommand == null)
                middleCommand = command;
            else
                sideCommands.Add(command);
        }

        if (middleCommand == null || sideCommands.Count != 2)
            return new List<GeometryCommand>(commands);

        return new List<GeometryCommand> { sideCommands[0], middleCommand, sideCommands[1] };
    }

    private void EnforceMinimumHorizontalSpacing(IList<GeometryCommand> commands)
    {
        List<GeometryCommand> sortedCommands = new List<GeometryCommand>(commands);
        sortedCommands.Sort((first, second) => first.PositionOffset.x.CompareTo(second.PositionOffset.x));

        float originalCenterX = GetAverageX(sortedCommands);
        float minimumSpacing = GetMinimumCenterSpacing(sortedCommands);

        for (int index = 1; index < sortedCommands.Count; index++)
        {
            GeometryCommand previous = sortedCommands[index - 1];
            GeometryCommand current = sortedCommands[index];
            float distance = current.PositionOffset.x - previous.PositionOffset.x;

            if (distance >= minimumSpacing)
                continue;

            current.PositionOffset = new Vector3(
                previous.PositionOffset.x + minimumSpacing,
                current.PositionOffset.y,
                current.PositionOffset.z);
        }

        float adjustedCenterX = GetAverageX(sortedCommands);
        float recenterOffset = originalCenterX - adjustedCenterX;

        for (int index = 0; index < sortedCommands.Count; index++)
            sortedCommands[index].PositionOffset += Vector3.right * recenterOffset;
    }

    private float GetMinimumCenterSpacing(IList<GeometryCommand> commands)
    {
        float largestObjectSize = settings.BaseObjectSize;

        for (int index = 0; index < commands.Count; index++)
        {
            GeometryCommand command = commands[index];
            if (command == null)
                continue;

            largestObjectSize = Mathf.Max(largestObjectSize, settings.BaseObjectSize * Mathf.Max(settings.MinScale, command.Scale));
        }

        return largestObjectSize + settings.DefaultObjectGap;
    }

    private float GetAverageX(IList<GeometryCommand> commands)
    {
        if (commands.Count == 0)
            return 0f;

        float totalX = 0f;
        int count = 0;

        for (int index = 0; index < commands.Count; index++)
        {
            GeometryCommand command = commands[index];
            if (command == null)
                continue;

            totalX += command.PositionOffset.x;
            count++;
        }

        return count == 0 ? 0f : totalX / count;
    }
}
