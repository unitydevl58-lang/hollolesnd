using System;
using Newtonsoft.Json.Linq;

/// <summary>
/// Extracts the JSON command text from a Gemini response envelope.
/// </summary>
public sealed class GeminiResponseParser
{
    public GeminiApiResult Parse(string rawResponse)
    {
        try
        {
            JObject response = JObject.Parse(rawResponse);
            string commandText = response["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();

            if (string.IsNullOrWhiteSpace(commandText))
                return GeminiApiResult.Failed("Gemini returned an empty command response.");

            return GeminiApiResult.Successful(commandText);
        }
        catch (Exception exception)
        {
            return GeminiApiResult.Failed($"Gemini response parse failed: {exception.Message}");
        }
    }
}
