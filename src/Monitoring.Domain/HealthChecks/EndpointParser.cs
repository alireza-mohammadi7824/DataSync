using System;
using System.Globalization;

namespace Monitoring.HealthChecks;

internal static class EndpointParser
{
    public static bool TryParseHostPort(string? value, out string host, out int port)
    {
        host = string.Empty;
        port = 0;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        var candidateHost = parts[0].Trim();
        if (string.IsNullOrWhiteSpace(candidateHost))
        {
            return false;
        }

        if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var candidatePort))
        {
            return false;
        }

        if (candidatePort is < 1 or > 65535)
        {
            return false;
        }

        host = candidateHost;
        port = candidatePort;
        return true;
    }
}
