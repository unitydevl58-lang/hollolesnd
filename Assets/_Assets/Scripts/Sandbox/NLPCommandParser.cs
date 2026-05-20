using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace HoloLensApp.Sandbox
{
    public enum WongOperation
    {
        Unknown,
        Detachment,   // Kopma
        Touching,     // Temas Etme
        Overlapping,  // Örtüşme
        Penetration,  // İçe Girme
        Union,        // Birleşme
        Subtraction,  // Eksilme
        Intersection, // Kesişme
        Coinciding    // Denk Gelme
    }

    public static class NLPCommandParser
    {
        private static readonly Dictionary<WongOperation, string[]> OperationKeywords = new Dictionary<WongOperation, string[]>
        {
            { WongOperation.Detachment, new[] { "kop", "ayrıl", "uzaklaş", "ayır", "kopar", "detach" } },
            { WongOperation.Touching, new[] { "temas", "dokun", "yaklaş", "değ", "touch", "yan yana" } },
            { WongOperation.Overlapping, new[] { "örtüş", "üstüne", "kapat", "ört", "overlap" } },
            { WongOperation.Penetration, new[] { "içine gir", "gir", "saplan", "geç", "penetrat" } },
            { WongOperation.Union, new[] { "birleş", "bütünleş", "kaynaş", "tek ol", "grup", "union", "merge", "topla" } },
            { WongOperation.Subtraction, new[] { "eksilt", "çıkar", "oy", "del", "kes", "subtract", "cut" } },
            { WongOperation.Intersection, new[] { "kesiş", "ortak", "sadece ortak", "intersect" } },
            { WongOperation.Coinciding, new[] { "denk", "aynı ol", "üst üste bin", "coincid" } }
        };

        public static WongOperation ParseCommand(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return WongOperation.Unknown;

            string normalized = input.ToLowerInvariant().Trim();

            foreach (var kvp in OperationKeywords)
            {
                foreach (var keyword in kvp.Value)
                {
                    if (normalized.Contains(keyword))
                    {
                        return kvp.Key;
                    }
                }
            }

            return WongOperation.Unknown;
        }

        public static string GetTurkishName(WongOperation op)
        {
            switch (op)
            {
                case WongOperation.Detachment: return "Kopma";
                case WongOperation.Touching: return "Temas Etme";
                case WongOperation.Overlapping: return "Örtüşme";
                case WongOperation.Penetration: return "İçe Girme";
                case WongOperation.Union: return "Birleşme";
                case WongOperation.Subtraction: return "Eksilme";
                case WongOperation.Intersection: return "Kesişme";
                case WongOperation.Coinciding: return "Denk Gelme";
                default: return "Bilinmeyen";
            }
        }
    }
}
