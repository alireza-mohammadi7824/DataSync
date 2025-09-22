using System.Collections.Generic;

namespace Monitoring.Endpoints;

public sealed record ParsedEndpoint(
    EndpointType Type,
    string Host,
    int? Port,
    string? Scheme,
    string? PathAndQuery,
    string? Raw,
    bool? IsSentinel,
    string? SentinelMasterName,
    int? Database,
    string? User,
    bool? Tls,
    IReadOnlyList<(string host, int port)>? SentinelNodes);
