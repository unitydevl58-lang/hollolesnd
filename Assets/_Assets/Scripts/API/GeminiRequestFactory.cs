using Newtonsoft.Json;

/// <summary>
/// Builds Gemini API request payloads.
/// </summary>
public sealed class GeminiRequestFactory
{
    public string BuildRequestJson(string prompt)
    {
        var requestBody = new
        {
            system_instruction = new
            {
                parts = new[] { new { text = GeminiPromptLibrary.SystemInstruction } }
            },
            contents = new[]
            {
                new
                {
                    parts = new[] { new { text = prompt } }
                }
            },
            generationConfig = new
            {
                response_mime_type = "application/json",
                temperature = 0.1f,
                maxOutputTokens = 1024
            }
        };

        return JsonConvert.SerializeObject(requestBody);
    }
}
