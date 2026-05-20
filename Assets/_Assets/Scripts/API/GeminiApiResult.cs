/// <summary>
/// Transport-level result returned by GeminiApiClient.
/// </summary>
public sealed class GeminiApiResult
{
    public bool Success;
    public string CommandJson;
    public string ErrorMessage;
    public long StatusCode;
    public bool IsRateLimited;
    public float RetryAfterSeconds;

    public static GeminiApiResult Successful(string commandJson)
    {
        return new GeminiApiResult
        {
            Success = true,
            CommandJson = commandJson
        };
    }

    public static GeminiApiResult Failed(string errorMessage, long statusCode = 0)
    {
        return new GeminiApiResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            StatusCode = statusCode
        };
    }
}
