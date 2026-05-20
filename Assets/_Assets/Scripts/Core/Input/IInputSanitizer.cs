/// <summary>
/// Applies text corrections before a prompt is sent to Gemini.
/// </summary>
public interface IInputSanitizer
{
    string Sanitize(string input);
}
