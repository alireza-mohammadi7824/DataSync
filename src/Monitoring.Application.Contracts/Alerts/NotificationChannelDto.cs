using System;

namespace Monitoring.Alerts;

public sealed class NotificationChannelDto
{
    public NotificationChannelType Type { get; init; }

    public string Destination { get; init; } = string.Empty;

    public bool Enabled { get; init; }
}
