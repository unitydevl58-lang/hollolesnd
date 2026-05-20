using System;

/// <summary>
/// Resolves the Gemini API key from an Inspector override or the GEMINI_API_KEY environment variable.
/// </summary>
public sealed class EnvironmentApiKeyProvider : IApiKeyProvider
{
    private const string GeminiApiKeyEnvironmentVariable = "GEMINI_API_KEY";

    public string ResolveApiKey(string inspectorOverride)
    {
        if (!string.IsNullOrWhiteSpace(inspectorOverride))
            return inspectorOverride.Trim();

        string environmentKey = Environment.GetEnvironmentVariable(GeminiApiKeyEnvironmentVariable);
        return string.IsNullOrWhiteSpace(environmentKey) ? null : environmentKey.Trim();
    }
}
