using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Monitoring.Execution;

public static class SettingsReader
{
    public static JsonElement? Get(string? json, string path)
    {
        if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var element = Navigate(document.RootElement, path);
            return element?.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static T? Get<T>(string? json, string path, T? @default = default)
    {
        var element = Get(json, path);
        if (element is null)
        {
            return @default;
        }

        try
        {
            if (typeof(T) == typeof(JsonElement))
            {
                return (T?)(object)element.Value.Clone();
            }

            if (typeof(T) == typeof(string))
            {
                return (T?)(object?)element.Value.ToString();
            }

            var raw = element.Value.GetRawText();
            var value = JsonSerializer.Deserialize<T>(raw);
            return value ?? @default;
        }
        catch (JsonException)
        {
            return @default;
        }
        catch (NotSupportedException)
        {
            return @default;
        }
    }

    private static JsonElement? Navigate(JsonElement root, string path)
    {
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            return null;
        }

        var current = root;
        foreach (var segment in segments)
        {
            if (!TryAdvance(ref current, segment))
            {
                return null;
            }
        }

        return current;
    }

    private static bool TryAdvance(ref JsonElement current, string segment)
    {
        if (segment.Length == 0)
        {
            return false;
        }

        var propertySegment = segment;
        var indexSegments = new List<int>();
        var bracketIndex = segment.IndexOf('[');
        if (bracketIndex >= 0)
        {
            propertySegment = segment[..bracketIndex];
            var remainder = segment[bracketIndex..];
            while (remainder.Length > 0)
            {
                if (remainder[0] != '[')
                {
                    return false;
                }

                var endBracket = remainder.IndexOf(']');
                if (endBracket < 0)
                {
                    return false;
                }

                var indexSpan = remainder.Substring(1, endBracket - 1);
                if (!int.TryParse(indexSpan, out var index) || index < 0)
                {
                    return false;
                }

                indexSegments.Add(index);
                remainder = remainder[(endBracket + 1)..];
            }
        }

        if (!string.IsNullOrEmpty(propertySegment))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(propertySegment, out var property))
            {
                return false;
            }

            current = property;
        }

        foreach (var index in indexSegments)
        {
            if (current.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            if (index >= current.GetArrayLength())
            {
                return false;
            }

            current = ElementAt(current, index);
        }

        return true;
    }

    private static JsonElement ElementAt(JsonElement array, int index)
    {
        var enumerator = array.EnumerateArray();
        var currentIndex = 0;
        while (enumerator.MoveNext())
        {
            if (currentIndex == index)
            {
                return enumerator.Current;
            }

            currentIndex++;
        }

        return default;
    }
}
