/// <summary>
/// Converts raw AI output into runtime geometry commands.
/// </summary>
public interface ICommandParser
{
    CommandParseResult Parse(string rawCommandData);
}
