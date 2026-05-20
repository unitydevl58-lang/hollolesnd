using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resolves English/Turkish color aliases and #RRGGBB values.
/// </summary>
public sealed class AliasColorResolver : IColorResolver
{
    private readonly Dictionary<string, Color> colors = new Dictionary<string, Color>();

    public AliasColorResolver()
    {
        RegisterAlias(new Color(0.90f, 0.18f, 0.18f), "red", "kirmizi", "kırmızı");
        RegisterAlias(new Color(0.15f, 0.47f, 0.90f), "blue", "mavi", "avi");
        RegisterAlias(new Color(0.97f, 0.80f, 0.10f), "yellow", "sari", "sarı");
        RegisterAlias(new Color(0.18f, 0.72f, 0.30f), "green", "yesil", "yeşil");
        RegisterAlias(Color.white, "white", "beyaz");
        RegisterAlias(Color.black, "black", "siyah");
        RegisterAlias(new Color(0.97f, 0.50f, 0.10f), "orange", "turuncu");
        RegisterAlias(new Color(0.58f, 0.18f, 0.85f), "purple", "mor");
        RegisterAlias(new Color(0.97f, 0.45f, 0.70f), "pink", "pembe");
        RegisterAlias(new Color(0.10f, 0.80f, 0.85f), "cyan");
        RegisterAlias(new Color(0.55f, 0.55f, 0.55f), "gray", "grey", "gri");
        RegisterAlias(new Color(0.55f, 0.30f, 0.10f), "brown", "kahverengi");
    }

    public void RegisterAlias(Color color, params string[] aliases)
    {
        for (int index = 0; index < aliases.Length; index++)
            colors[TextNormalizer.NormalizeKey(aliases[index])] = color;
    }

    public bool TryResolve(string rawColor, out Color color)
    {
        color = Color.white;

        if (string.IsNullOrWhiteSpace(rawColor))
            return true;

        string normalized = TextNormalizer.NormalizeKey(rawColor);
        if (colors.TryGetValue(normalized, out color))
            return true;

        string htmlCandidate = rawColor.Trim();
        if (!htmlCandidate.StartsWith("#", StringComparison.Ordinal))
            htmlCandidate = "#" + htmlCandidate;

        return ColorUtility.TryParseHtmlString(htmlCandidate, out color);
    }
}
