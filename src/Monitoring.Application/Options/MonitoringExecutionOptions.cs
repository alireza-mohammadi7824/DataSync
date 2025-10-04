namespace Monitoring.Options;

/// <summary>
/// Execution pipeline options. Values can be supplied via configuration using
/// the <c>Monitoring:Execution</c> section or the corresponding environment
/// variables (e.g. <c>MONITORING__EXECUTION__MAXCONCURRENTCHECKS</c>).
/// </summary>
public sealed class MonitoringExecutionOptions
{
    public int MaxConcurrentChecks { get; set; } = 8;

    public int LockTtlBufferSeconds { get; set; } = 5;

    public int? GlobalCheckTimeoutSeconds { get; set; }
        = null;

    public int MaxRetryAttempts { get; set; } = 2;

    public int MaxBackoffSeconds { get; set; } = 30;
}
