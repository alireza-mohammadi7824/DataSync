namespace Monitoring.Execution;

public sealed record HealthCheckResult(
    bool IsSuccess,
    int? ResponseTimeMs,
    string? ErrorSummary,
    string TriggerSource);
