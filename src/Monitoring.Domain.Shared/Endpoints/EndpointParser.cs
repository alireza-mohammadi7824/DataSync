using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Monitoring.Endpoints;

public static class EndpointParser
{
    public static bool TryParse(string input, EndpointType hintedType, out ParsedEndpoint? ep, out string? error)
    {
        ep = null;
        error = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Endpoint is required.";
            return false;
        }

        input = input.Trim();
        var type = DetermineType(input, hintedType);

        return type switch
        {
            EndpointType.Api => TryParseHttp(input, EndpointType.Api, out ep, out error),
            EndpointType.Website => TryParseHttp(input, EndpointType.Website, out ep, out error),
            EndpointType.Tcp => TryParseTcp(input, out ep, out error),
            EndpointType.Redis => TryParseRedis(input, out ep, out error),
            _ => Fail("Unsupported endpoint type.", out ep, out error)
        };
    }

    private static EndpointType DetermineType(string input, EndpointType hint)
    {
        var normalized = input.TrimStart();
        var schemeSeparator = normalized.IndexOf("://", StringComparison.Ordinal);
        if (schemeSeparator > 0)
        {
            var scheme = normalized[..schemeSeparator].ToLowerInvariant();
            if (scheme is "http" or "https")
            {
                return hint is EndpointType.Api ? EndpointType.Api : EndpointType.Website;
            }

            if (scheme is "redis" or "rediss")
            {
                return EndpointType.Redis;
            }
        }

        if (normalized.Contains(';'))
        {
            return EndpointType.Redis;
        }

        if (hint is EndpointType.Redis)
        {
            return EndpointType.Redis;
        }

        if (hint is EndpointType.Api or EndpointType.Website)
        {
            return hint;
        }

        if (normalized.Contains(':'))
        {
            return hint == EndpointType.Redis ? EndpointType.Redis : EndpointType.Tcp;
        }

        return hint;
    }

    private static bool TryParseHttp(string input, EndpointType type, out ParsedEndpoint? ep, out string? error)
    {
        ep = null;
        error = null;

        var url = input;
        if (!url.Contains("://", StringComparison.Ordinal))
        {
            url = "https://" + url;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            !(uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
              uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)))
        {
            error = "Invalid HTTP(S) endpoint.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            error = "Endpoint host is required.";
            return false;
        }

        var host = uri.Host.ToLowerInvariant();
        int? port = uri.IsDefaultPort ? null : uri.Port;
        var pathAndQuery = uri.PathAndQuery;
        if (string.IsNullOrEmpty(pathAndQuery))
        {
            pathAndQuery = "/";
        }

        ep = new ParsedEndpoint(
            type,
            host,
            port,
            uri.Scheme.ToLowerInvariant(),
            pathAndQuery,
            input,
            null,
            null,
            null,
            null,
            null,
            null);
        return true;
    }

    private static bool TryParseTcp(string input, out ParsedEndpoint? ep, out string? error)
    {
        ep = null;
        error = null;

        if (!TryParseHostPort(input, requirePort: true, out var host, out var port, out error))
        {
            return false;
        }

        ep = new ParsedEndpoint(
            EndpointType.Tcp,
            host,
            port,
            null,
            null,
            input,
            null,
            null,
            null,
            null,
            null,
            null);
        return true;
    }

    private static bool TryParseRedis(string input, out ParsedEndpoint? ep, out string? error)
    {
        ep = null;
        error = null;

        if (input.Contains(';'))
        {
            return TryParseRedisSentinel(input, out ep, out error);
        }

        if (input.Contains("://", StringComparison.Ordinal))
        {
            return TryParseRedisUrl(input, out ep, out error);
        }

        return TryParseRedisHostPort(input, out ep, out error);
    }

    private static bool TryParseRedisHostPort(string input, out ParsedEndpoint? ep, out string? error)
    {
        ep = null;
        error = null;

        var trimmed = input.Trim();
        var queryIndex = trimmed.IndexOf('?', StringComparison.Ordinal);
        var endpointPart = queryIndex >= 0 ? trimmed[..queryIndex] : trimmed;
        var queryPart = queryIndex >= 0 ? trimmed[(queryIndex + 1)..] : string.Empty;

        if (!TryParseHostPort(endpointPart, requirePort: false, out var host, out var port, out error))
        {
            return false;
        }

        var options = ParseQuery(queryPart);
        var database = ParseDatabase(options, out error);
        if (error != null)
        {
            ep = null;
            return false;
        }

        var user = options.TryGetValue("user", out var userValue) ? NullIfEmpty(userValue) : null;
        var tls = ParseTls(options, defaultValue: null, out error);
        if (error != null)
        {
            ep = null;
            return false;
        }

        ep = new ParsedEndpoint(
            EndpointType.Redis,
            host,
            port,
            null,
            null,
            input,
            false,
            null,
            database,
            user,
            tls,
            null);
        return true;
    }

    private static bool TryParseRedisUrl(string input, out ParsedEndpoint? ep, out string? error)
    {
        ep = null;
        error = null;

        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri) ||
            !(uri.Scheme.Equals("redis", StringComparison.OrdinalIgnoreCase) ||
              uri.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase)))
        {
            error = "Invalid Redis URL.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            error = "Redis host is required.";
            return false;
        }

        var host = uri.Host.ToLowerInvariant();
        int? port = uri.IsDefaultPort ? null : uri.Port;
        var options = ParseQuery(uri.Query);
        var database = ParseDatabase(options, out error);
        if (error != null)
        {
            ep = null;
            return false;
        }

        if (uri.AbsolutePath.Length > 1 && database == null)
        {
            var path = uri.AbsolutePath.Trim('/');
            if (path.Length > 0 && int.TryParse(path, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pathDb) && pathDb >= 0)
            {
                database = pathDb;
            }
        }

        var user = ExtractUser(uri.UserInfo, options);
        var tls = ParseTls(options, uri.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase) ? true : (bool?)null, out error);
        if (error != null)
        {
            ep = null;
            return false;
        }

        ep = new ParsedEndpoint(
            EndpointType.Redis,
            host,
            port,
            uri.Scheme.ToLowerInvariant(),
            null,
            input,
            false,
            null,
            database,
            user,
            tls,
            null);
        return true;
    }

    private static bool TryParseRedisSentinel(string input, out ParsedEndpoint? ep, out string? error)
    {
        ep = null;
        error = null;

        var segments = input.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 2)
        {
            error = "Sentinel configuration requires nodes and master name.";
            return false;
        }

        var nodeSegment = segments[0];
        var masterName = segments[1].Trim();
        if (string.IsNullOrWhiteSpace(masterName))
        {
            error = "Sentinel master name is required.";
            return false;
        }

        var nodes = new List<(string host, int port)>();
        foreach (var candidate in nodeSegment.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TryParseHostPort(candidate, requirePort: true, out var host, out var port, out error))
            {
                ep = null;
                return false;
            }

            nodes.Add((host, port!.Value));
        }

        if (nodes.Count == 0)
        {
            error = "At least one sentinel node is required.";
            return false;
        }

        int? database = null;
        string? user = null;
        bool? tls = null;

        foreach (var segment in segments.Skip(2))
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                continue;
            }

            var kv = segment.Split('=', 2);
            if (kv.Length != 2)
            {
                error = $"Invalid sentinel option '{segment}'.";
                return false;
            }

            var key = kv[0].Trim().ToLowerInvariant();
            var value = kv[1].Trim();
            switch (key)
            {
                case "db":
                    var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["db"] = value
                    };
                    database = ParseDatabase(options, out error);
                    if (error != null)
                    {
                        return false;
                    }
                    break;
                case "user":
                    user = NullIfEmpty(value);
                    break;
                case "tls":
                case "ssl":
                    var flagOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        [key] = value
                    };
                    tls = ParseTls(flagOptions, tls, out error);
                    if (error != null)
                    {
                        return false;
                    }
                    break;
            }
        }

        var primary = nodes[0];
        ep = new ParsedEndpoint(
            EndpointType.Redis,
            primary.host,
            primary.port,
            null,
            null,
            input,
            true,
            masterName,
            database,
            user,
            tls,
            nodes);
        return true;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(query))
        {
            return result;
        }

        if (query.StartsWith('?', StringComparison.Ordinal))
        {
            query = query[1..];
        }

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            if (kv.Length == 0)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(kv[0].Trim());
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            var value = kv.Length > 1 ? Uri.UnescapeDataString(kv[1].Trim()) : string.Empty;
            result[key.ToLowerInvariant()] = value;
        }

        return result;
    }

    private static int? ParseDatabase(Dictionary<string, string> options, out string? error)
    {
        error = null;
        if (!options.TryGetValue("db", out var value) || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var db) || db < 0)
        {
            error = "Invalid Redis database index.";
            return null;
        }

        return db;
    }

    private static bool? ParseTls(Dictionary<string, string> options, bool? defaultValue, out string? error)
    {
        error = null;
        if (!options.TryGetValue("tls", out var value) && !options.TryGetValue("ssl", out value))
        {
            return defaultValue;
        }

        value = value.Trim();
        if (bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        if (value.Equals("1", StringComparison.Ordinal) || value.Equals("yes", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (value.Equals("0", StringComparison.Ordinal) || value.Equals("no", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        error = "Invalid Redis TLS flag.";
        return null;
    }

    private static string? ExtractUser(string userInfo, Dictionary<string, string> options)
    {
        if (!string.IsNullOrEmpty(userInfo))
        {
            var separator = userInfo.IndexOf(':');
            return separator >= 0 ? NullIfEmpty(userInfo[..separator]) : NullIfEmpty(userInfo);
        }

        return options.TryGetValue("user", out var user) ? NullIfEmpty(user) : null;
    }

    private static bool TryParseHostPort(string value, bool requirePort, out string host, out int? port, out string? error)
    {
        host = string.Empty;
        port = null;
        error = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            error = "Endpoint must include a host.";
            return false;
        }

        value = value.Trim();
        string hostPart;
        string? portPart = null;

        if (value.StartsWith('['))
        {
            var endIndex = value.IndexOf(']');
            if (endIndex <= 0)
            {
                error = "Invalid IPv6 endpoint.";
                return false;
            }

            hostPart = value[1..endIndex];
            var remainder = value[(endIndex + 1)..];
            if (remainder.StartsWith(':'))
            {
                portPart = remainder[1..];
            }
            else if (!string.IsNullOrEmpty(remainder))
            {
                error = "Invalid IPv6 endpoint.";
                return false;
            }
        }
        else
        {
            var colonIndex = value.LastIndexOf(':');
            if (colonIndex > 0 && value.IndexOf(':') == colonIndex)
            {
                hostPart = value[..colonIndex];
                portPart = value[(colonIndex + 1)..];
            }
            else if (colonIndex < 0)
            {
                hostPart = value;
            }
            else
            {
                error = "Invalid endpoint host.";
                return false;
            }
        }

        host = hostPart.Trim().Trim('[', ']').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(host))
        {
            error = "Endpoint host is required.";
            return false;
        }

        if (portPart == null)
        {
            if (requirePort)
            {
                error = "Endpoint port is required.";
                return false;
            }

            return true;
        }

        if (!int.TryParse(portPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPort) ||
            parsedPort is < 1 or > 65535)
        {
            error = "Endpoint port must be between 1 and 65535.";
            return false;
        }

        port = parsedPort;
        return true;
    }

    private static bool Fail(string message, out ParsedEndpoint? endpoint, out string? error)
    {
        endpoint = null;
        error = message;
        return false;
    }

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
