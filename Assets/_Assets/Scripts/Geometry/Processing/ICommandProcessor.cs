/// <summary>
/// Full geometry command pipeline: parse, sanitize, validate, enrich, and layout.
/// </summary>
public interface ICommandProcessor
{
    GeometryCommandProcessingResult Process(string rawCommandData, SymbolicAnalysisResult symbolicAnalysis);
}
