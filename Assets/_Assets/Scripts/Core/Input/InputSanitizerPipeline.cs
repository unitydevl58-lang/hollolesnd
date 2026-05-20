using System.Collections.Generic;

/// <summary>
/// Runs a sequence of sanitizer rules without coupling the caller to specific corrections.
/// </summary>
public sealed class InputSanitizerPipeline : IInputSanitizer
{
    private readonly List<IInputSanitizerRule> rules = new List<IInputSanitizerRule>();

    public InputSanitizerPipeline(IEnumerable<IInputSanitizerRule> initialRules)
    {
        if (initialRules == null)
            return;

        foreach (IInputSanitizerRule rule in initialRules)
        {
            if (rule != null)
                rules.Add(rule);
        }
    }

    public string Sanitize(string input)
    {
        string sanitized = input ?? string.Empty;

        for (int index = 0; index < rules.Count; index++)
            sanitized = rules[index].Apply(sanitized);

        return sanitized.Trim();
    }
}
