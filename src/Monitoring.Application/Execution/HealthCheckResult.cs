using System;

namespace Monitoring.Execution;

public sealed record HealthCheckResult(
    bool IsSuccess,
    int? ResponseTimeMs,
    string? ErrorSummary,
    string TriggerSource)
{
    public bool IsSkipped { get; init; }

    public string? SkipReason { get; init; }

    public DateTime CompletedAt { get; init; }

    public static HealthCheckResult CreateSkipped(string triggerSource, string reason, DateTime completedAt)
        => new(false, null, reason, triggerSource)
        {
            IsSkipped = true,
            SkipReason = reason,
            CompletedAt = completedAt
        };
}
