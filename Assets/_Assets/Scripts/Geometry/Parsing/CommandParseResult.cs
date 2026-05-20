using System.Collections.Generic;

/// <summary>
/// Result object returned by geometry command parsers.
/// </summary>
public sealed class CommandParseResult
{
    public bool Success;
    public string ErrorMessage;
    public readonly List<GeometryCommand> Commands = new List<GeometryCommand>();

    public static CommandParseResult Failed(string message)
    {
        return new CommandParseResult
        {
            Success = false,
            ErrorMessage = message
        };
    }

    public static CommandParseResult Succeeded(List<GeometryCommand> commands)
    {
        CommandParseResult result = new CommandParseResult { Success = true };

        if (commands != null)
            result.Commands.AddRange(commands);

        return result;
    }
}
