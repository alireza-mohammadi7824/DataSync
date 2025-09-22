using System;
using System.Threading;
using System.Threading.Tasks;
using Monitoring.HealthChecks;
using Monitoring.Targets;

namespace Monitoring.Execution;

public interface IMonitoringCheckService
{
    Task<CheckProcessingResult> RunAsync(
        MonitoringTarget target,
        string triggerSource,
        bool dispatchAlerts,
        CancellationToken cancellationToken);
}

public sealed record CheckProcessingResult
{
    private CheckProcessingResult(bool isSkipped, string? skipReason, HealthCheckResult? result, DateTime? completedAt, int alertsDispatched, MonitoringCheckOutcome? outcome)
    {
        IsSkipped = isSkipped;
        SkipReason = skipReason;
        Result = result;
        CompletedAt = completedAt;
        AlertsDispatched = alertsDispatched;
        Outcome = outcome;
    }

    public bool IsSkipped { get; }

    public string? SkipReason { get; }

    public HealthCheckResult? Result { get; }

    public DateTime? CompletedAt { get; }

    public int AlertsDispatched { get; }

    public MonitoringCheckOutcome? Outcome { get; }

    public bool IsSuccess => !IsSkipped && Result?.IsSuccess == true;

    public static CheckProcessingResult Skipped(string reason)
        => new(true, reason, null, null, 0, null);

    public static CheckProcessingResult Completed(HealthCheckResult result, DateTime completedAt, int alertsDispatched, MonitoringCheckOutcome outcome)
        => new(false, null, result, completedAt, alertsDispatched, outcome);
}
