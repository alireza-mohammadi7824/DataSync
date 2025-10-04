using System;
using System.Collections.Generic;

namespace Monitoring.Alerts;

public sealed class AlertNotificationDto
{
    public Guid TargetId { get; init; }

    public string TargetName { get; init; } = string.Empty;

    public string AlertName { get; init; } = string.Empty;

    public string Severity { get; init; } = string.Empty;

    public DateTime TriggeredAtUtc { get; init; }

    public string? Message { get; init; }

    public IReadOnlyList<NotificationChannelDto> Channels { get; init; }
        = Array.Empty<NotificationChannelDto>();
}
