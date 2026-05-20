using System;
using UnityEngine;

/// <summary>
/// Extracts a JSON payload from raw model text, including fenced Markdown responses.
/// </summary>
public sealed class JsonPayloadExtractor
{
    public string Extract(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return string.Empty;

        string trimmed = rawText.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            int firstLineBreak = trimmed.IndexOf('\n');
            int closingFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (firstLineBreak >= 0 && closingFence > firstLineBreak)
                trimmed = trimmed.Substring(firstLineBreak + 1, closingFence - firstLineBreak - 1).Trim();
        }

        int firstArray = trimmed.IndexOf('[');
        int firstObject = trimmed.IndexOf('{');
        int startIndex = -1;

        if (firstArray >= 0 && firstObject >= 0)
            startIndex = Mathf.Min(firstArray, firstObject);
        else if (firstArray >= 0)
            startIndex = firstArray;
        else if (firstObject >= 0)
            startIndex = firstObject;

        if (startIndex < 0)
            return trimmed;

        char startChar = trimmed[startIndex];
        char endChar = startChar == '[' ? ']' : '}';
        int endIndex = trimmed.LastIndexOf(endChar);

        return endIndex >= startIndex
            ? trimmed.Substring(startIndex, endIndex - startIndex + 1)
            : trimmed.Substring(startIndex);
    }
}
