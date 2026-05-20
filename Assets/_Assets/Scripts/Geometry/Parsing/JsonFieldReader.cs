using System;
using System.Globalization;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Safe helpers for reading aliased JSON fields.
/// </summary>
public static class JsonFieldReader
{
    public static JToken GetProperty(JObject source, params string[] aliases)
    {
        if (source == null)
            return null;

        foreach (JProperty property in source.Properties())
        {
            for (int index = 0; index < aliases.Length; index++)
            {
                if (string.Equals(property.Name, aliases[index], StringComparison.OrdinalIgnoreCase))
                    return property.Value;
            }
        }

        return null;
    }

    public static string ReadString(JObject source, params string[] aliases)
    {
        JToken token = GetProperty(source, aliases);
        return token == null || token.Type == JTokenType.Null ? null : token.ToString();
    }

    public static int ReadInt(JObject source, int fallback, params string[] aliases)
    {
        JToken token = GetProperty(source, aliases);
        if (token == null)
            return fallback;

        if (token.Type == JTokenType.Integer)
            return token.Value<int>();

        return int.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : fallback;
    }

    public static float ReadFloat(JObject source, float fallback, params string[] aliases)
    {
        return ReadFloatToken(GetProperty(source, aliases), fallback);
    }

    public static bool ReadBool(JObject source, bool fallback, params string[] aliases)
    {
        JToken token = GetProperty(source, aliases);
        if (token == null)
            return fallback;

        if (token.Type == JTokenType.Boolean)
            return token.Value<bool>();

        return bool.TryParse(token.ToString(), out bool value) ? value : fallback;
    }

    public static Vector3 ReadVector3(JObject source, params string[] aliases)
    {
        JToken token = GetProperty(source, aliases);
        if (token is JArray array && array.Count >= 3)
        {
            return new Vector3(
                ReadFloatToken(array[0], 0f),
                ReadFloatToken(array[1], 0f),
                ReadFloatToken(array[2], 0f));
        }

        if (token is JObject vectorObject)
        {
            return new Vector3(
                ReadFloatToken(GetProperty(vectorObject, "x"), 0f),
                ReadFloatToken(GetProperty(vectorObject, "y"), 0f),
                ReadFloatToken(GetProperty(vectorObject, "z"), 0f));
        }

        return Vector3.zero;
    }

    public static float ReadFloatToken(JToken token, float fallback)
    {
        if (token == null)
            return fallback;

        if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer)
            return token.Value<float>();

        return float.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            ? value
            : fallback;
    }
}
