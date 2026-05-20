/// <summary>
/// Normalizes user and model text so Turkish and English aliases can be matched reliably.
/// </summary>
public static class TextNormalizer
{
    public static string NormalizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Trim()
            .Replace("İ", "I")
            .Replace("ı", "i")
            .Replace("ş", "s")
            .Replace("Ş", "S")
            .Replace("ğ", "g")
            .Replace("Ğ", "G")
            .Replace("ü", "u")
            .Replace("Ü", "U")
            .Replace("ö", "o")
            .Replace("Ö", "O")
            .Replace("ç", "c")
            .Replace("Ç", "C")
            .Replace("'", string.Empty)
            .Replace("’", string.Empty)
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .ToLowerInvariant();
    }
}
