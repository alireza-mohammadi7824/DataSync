using System;

namespace Monitoring.Alerts;

public sealed class AlertPayload
{
    public Guid TargetId { get; }

    public string TargetName { get; }

    public string Status { get; }

    public DateTime StartedAt { get; }

    public DateTime? EndedAt { get; }

    public TimeSpan? Duration { get; }

    public string EventType { get; }

    public string Summary { get; }

    public AlertPayload(
        Guid targetId,
        string targetName,
        string status,
        DateTime startedAt,
        DateTime? endedAt,
        TimeSpan? duration,
        string eventType,
        string summary)
    {
        TargetId = targetId;
        TargetName = targetName;
        Status = status;
        StartedAt = startedAt;
        EndedAt = endedAt;
        Duration = duration;
        EventType = eventType;
        Summary = summary;
    }
}
