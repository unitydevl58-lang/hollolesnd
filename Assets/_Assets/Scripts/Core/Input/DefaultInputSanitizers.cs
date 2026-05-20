using System.Collections.Generic;

/// <summary>
/// Factory for the project-default prompt sanitizer pipeline.
/// New typo or synonym rules can be added here without changing request flow code.
/// </summary>
public static class DefaultInputSanitizers
{
    public static IInputSanitizer Create()
    {
        return new InputSanitizerPipeline(new List<IInputSanitizerRule>
        {
            new RegexReplacementSanitizerRule(@"\bavi\b", "mavi")
        });
    }
}
