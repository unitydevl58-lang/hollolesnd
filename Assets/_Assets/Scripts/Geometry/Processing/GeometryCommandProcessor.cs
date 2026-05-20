using System.Collections.Generic;

/// <summary>
/// Coordinates command parsing, validation, semantic partitioning, and layout.
/// </summary>
public sealed class GeometryCommandProcessor : ICommandProcessor
{
    private readonly ICommandParser parser;
    private readonly GeometryCommandValidator validator;
    private readonly SemanticPartitionPlanner partitionPlanner;
    private readonly GeometrySceneLayoutService layoutService;

    public GeometryCommandProcessor(
        ICommandParser parser,
        GeometryCommandValidator validator,
        SemanticPartitionPlanner partitionPlanner,
        GeometrySceneLayoutService layoutService)
    {
        this.parser = parser;
        this.validator = validator;
        this.partitionPlanner = partitionPlanner;
        this.layoutService = layoutService;
    }

    public GeometryCommandProcessingResult Process(string rawCommandData, SymbolicAnalysisResult symbolicAnalysis)
    {
        CommandParseResult parseResult = parser.Parse(rawCommandData);
        if (!parseResult.Success)
            return GeometryCommandProcessingResult.Failed(parseResult.ErrorMessage);

        GeometryCommandProcessingResult result = new GeometryCommandProcessingResult { Success = true };
        List<GeometryCommand> commands = parseResult.Commands;

        for (int index = commands.Count - 1; index >= 0; index--)
        {
            GeometryCommand command = commands[index];
            if (command == null)
            {
                commands.RemoveAt(index);
                continue;
            }

            if (!validator.IsCreateAction(command.Action))
            {
                result.Warnings.Add($"Unsupported action '{command.Action}' was skipped.");
                commands.RemoveAt(index);
            }
        }

        if (commands.Count == 0)
            return GeometryCommandProcessingResult.Failed("No create command was found.");

        partitionPlanner.Apply(commands, symbolicAnalysis);
        validator.Normalize(commands);
        layoutService.Apply(commands);
        result.Commands.AddRange(commands);

        return result;
    }
}
