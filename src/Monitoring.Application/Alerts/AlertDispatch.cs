using System;
using System.Collections.Generic;

namespace Monitoring.Alerts;

public sealed record AlertDispatch(
    string Channel,
    string TargetSnapshot,
    string Payload,
    IReadOnlyList<string> Recipients,
    string? Destination,
    string Summary)
{
    public static AlertDispatch Create(string channel, string targetSnapshot, string payload)
        => new(channel, targetSnapshot, payload, Array.Empty<string>(), null, targetSnapshot);

    public static AlertDispatch Create(
        string channel,
        string targetSnapshot,
        string payload,
        IReadOnlyList<string> recipients,
        string? destination,
        string summary)
        => new(channel, targetSnapshot, payload, recipients, destination, summary);
}
