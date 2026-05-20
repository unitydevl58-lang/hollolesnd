/// <summary>
/// Resolves a runtime API key from allowed sources.
/// </summary>
public interface IApiKeyProvider
{
    string ResolveApiKey(string inspectorOverride);
}
