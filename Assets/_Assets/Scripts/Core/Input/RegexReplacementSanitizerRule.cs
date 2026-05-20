using System.Text.RegularExpressions;

/// <summary>
/// Replaces a text pattern with a canonical value.
/// </summary>
public sealed class RegexReplacementSanitizerRule : IInputSanitizerRule
{
    private readonly Regex regex;
    private readonly string replacement;

    public RegexReplacementSanitizerRule(string pattern, string replacementText)
    {
        regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        replacement = replacementText;
    }

    public string Apply(string input)
    {
        return string.IsNullOrEmpty(input) ? input : regex.Replace(input, replacement);
    }
}
