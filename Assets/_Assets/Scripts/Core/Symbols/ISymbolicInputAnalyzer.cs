/// <summary>
/// Deconstructs a natural language prompt into reusable symbolic components.
/// </summary>
public interface ISymbolicInputAnalyzer
{
    SymbolicAnalysisResult Analyze(string input);
}
