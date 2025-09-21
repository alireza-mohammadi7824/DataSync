using System;
using Monitoring.Targets;

namespace Monitoring.Alerts;

public enum AlertEventType
{
    Down,
    StillDown,
    Recovered
}

public sealed class TargetSnapshot
{
    public TargetSnapshot(
        Guid targetId,
        string name,
        ServiceType type,
        string endpoint,
        ServiceStatus status,
        DateTime? lastCheckedAt,
        DateTime? lastStatusChangeAt,
        DateTime? firstDownAt,
        DateTime? lastUpAt,
        string? category)
    {
        TargetId = targetId;
        Name = name;
        Type = type;
        Endpoint = endpoint;
        Status = status;
        LastCheckedAt = lastCheckedAt;
        LastStatusChangeAt = lastStatusChangeAt;
        FirstDownAt = firstDownAt;
        LastUpAt = lastUpAt;
        Category = category;
    }

    public Guid TargetId { get; }
    public string Name { get; }
    public ServiceType Type { get; }
    public string Endpoint { get; }
    public ServiceStatus Status { get; }
    public DateTime? LastCheckedAt { get; }
    public DateTime? LastStatusChangeAt { get; }
    public DateTime? FirstDownAt { get; }
    public DateTime? LastUpAt { get; }
    public string? Category { get; }
}

public sealed class AlertPayload
{
    public AlertPayload(
        AlertEventType eventType,
        DateTime eventAt,
        string? errorSummary,
        int? responseTimeMs,
        OutageSnapshot? currentOutage)
    {
        EventType = eventType;
        EventAt = eventAt;
        ErrorSummary = errorSummary;
        ResponseTimeMs = responseTimeMs;
        CurrentOutage = currentOutage;
    }

    public AlertEventType EventType { get; }
    public DateTime EventAt { get; }
    public string? ErrorSummary { get; }
    public int? ResponseTimeMs { get; }
    public OutageSnapshot? CurrentOutage { get; }
}

public sealed class OutageSnapshot
{
    public OutageSnapshot(
        Guid outageId,
        DateTime startedAt,
        DateTime? endedAt,
        int failureCount,
        int? totalDurationSec)
    {
        OutageId = outageId;
        StartedAt = startedAt;
        EndedAt = endedAt;
        FailureCount = failureCount;
        TotalDurationSec = totalDurationSec;
    }

    public Guid OutageId { get; }
    public DateTime StartedAt { get; }
    public DateTime? EndedAt { get; }
    public int FailureCount { get; }
    public int? TotalDurationSec { get; }
}
