/// <summary>
/// A single, composable input correction rule.
/// </summary>
public interface IInputSanitizerRule
{
    string Apply(string input);
}
