using System.Collections.Generic;

/// <summary>
/// Final command-processing result consumed by GeometryManager.
/// </summary>
public sealed class GeometryCommandProcessingResult
{
    public bool Success;
    public string ErrorMessage;
    public readonly List<string> Warnings = new List<string>();
    public readonly List<GeometryCommand> Commands = new List<GeometryCommand>();

    public static GeometryCommandProcessingResult Failed(string message)
    {
        return new GeometryCommandProcessingResult
        {
            Success = false,
            ErrorMessage = message
        };
    }
}
