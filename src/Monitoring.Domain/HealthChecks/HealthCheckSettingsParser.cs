using System;
using System.Text.Json;

namespace Monitoring.HealthChecks;

internal static class HealthCheckSettingsParser
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static WebsiteSettings ParseWebsite(string? json)
    {
        return Deserialize(json, new WebsiteSettings());
    }

    public static ApiSettings ParseApi(string? json)
    {
        return Deserialize(json, new ApiSettings());
    }

    public static TcpSettings ParseTcp(string? json)
    {
        return Deserialize(json, new TcpSettings());
    }

    public static RedisSettings ParseRedis(string? json)
    {
        return Deserialize(json, new RedisSettings());
    }

    private static T Deserialize<T>(string? json, T fallback)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return fallback;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, SerializerOptions) ?? fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }
}
