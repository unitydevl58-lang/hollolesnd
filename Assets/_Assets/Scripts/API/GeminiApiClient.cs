using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

/// <summary>
/// Thin HTTP client responsible only for Gemini transport.
/// </summary>
public sealed class GeminiApiClient
{
    private const string GeminiUrlTemplate =
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key=";

    private readonly GeminiRequestFactory requestFactory;
    private readonly GeminiResponseParser responseParser;

    public GeminiApiClient(GeminiRequestFactory requestFactory, GeminiResponseParser responseParser)
    {
        this.requestFactory = requestFactory;
        this.responseParser = responseParser;
    }

    public async Task<GeminiApiResult> GenerateGeometryJsonAsync(string prompt, string apiKey, CancellationToken cancellationToken)
    {
        string requestUrl = GeminiUrlTemplate + UnityWebRequest.EscapeURL(apiKey);
        string requestJson = requestFactory.BuildRequestJson(prompt);
        byte[] requestBytes = Encoding.UTF8.GetBytes(requestJson);

        using (UnityWebRequest request = new UnityWebRequest(requestUrl, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(requestBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }

            if (request.result == UnityWebRequest.Result.Success)
                return responseParser.Parse(request.downloadHandler.text);

            GeminiApiResult failedResult = GeminiApiResult.Failed(BuildHttpErrorMessage(request), request.responseCode);

            if (request.responseCode == 429)
            {
                failedResult.IsRateLimited = true;
                failedResult.RetryAfterSeconds = ReadRetryAfterSeconds(request);
            }

            return failedResult;
        }
    }

    private string BuildHttpErrorMessage(UnityWebRequest request)
    {
        switch (request.responseCode)
        {
            case 400:
                return "Geçersiz istek (HTTP 400). Komut formatını kontrol edin.";
            case 401:
                return "API anahtarı geçersiz (HTTP 401).";
            case 403:
                return "API anahtarı yetkisiz (HTTP 403).";
            case 429:
                return "API kotası doldu (HTTP 429).";
            case 500:
                return "Gemini sunucu hatası (HTTP 500). Tekrar deneyin.";
            case 503:
                return "Gemini şu an meşgul (HTTP 503). Bir süre bekleyin.";
            default:
                return $"Bağlantı hatası ({request.responseCode}): {request.error}";
        }
    }

    private float ReadRetryAfterSeconds(UnityWebRequest request)
    {
        string retryAfterHeader = request.GetResponseHeader("Retry-After");

        return float.TryParse(retryAfterHeader, NumberStyles.Float, CultureInfo.InvariantCulture, out float retryAfterSeconds)
            ? retryAfterSeconds
            : 0f;
    }
}
