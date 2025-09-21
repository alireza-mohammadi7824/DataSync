using System.Collections.Generic;

namespace Monitoring.HealthChecks;

public sealed class WebsiteSettings
{
    public string? ContainsKeyword { get; set; }

    public int[]? ExpectedStatusCodes { get; set; }

    public string? Method { get; set; }
}

public sealed class ApiSettings
{
    public int[]? ExpectedStatusCodes { get; set; }

    public string? ContainsKeyword { get; set; }

    public Dictionary<string, string>? Headers { get; set; }

    public ApiAuthOptions? Auth { get; set; }

    public string? Method { get; set; }

    public string? Body { get; set; }
}

public sealed class ApiAuthOptions
{
    public string? Scheme { get; set; }

    public string? TokenRef { get; set; }

    public string? UsernameRef { get; set; }

    public string? PasswordRef { get; set; }
}

public sealed class TcpSettings
{
    public string? Host { get; set; }

    public int? Port { get; set; }
}
