/// <summary>
/// One symbolic unit extracted from a natural language command.
/// </summary>
public sealed class CommandSymbol
{
    public CommandSymbolType Type;
    public string RawText;
    public string NormalizedText;
    public int StartIndex;
    public int Length;
}
