using System;

namespace Monitoring.Config;

public sealed class MonitoringConfigDto
{
    public MonitoringConfigDto(
        MonitoringExecutionConfigDto execution,
        MonitoringRetentionConfigDto retention,
        MonitoringAlertsConfigDto alerts)
    {
        Execution = execution ?? throw new ArgumentNullException(nameof(execution));
        Retention = retention ?? throw new ArgumentNullException(nameof(retention));
        Alerts = alerts ?? throw new ArgumentNullException(nameof(alerts));
    }

    public MonitoringExecutionConfigDto Execution { get; }

    public MonitoringRetentionConfigDto Retention { get; }

    public MonitoringAlertsConfigDto Alerts { get; }
}

public sealed class MonitoringExecutionConfigDto
{
    public int MaxConcurrentChecks { get; init; }

    public int LockTtlBufferSeconds { get; init; }

    public int? GlobalCheckTimeoutSeconds { get; init; }

    public int MaxRetryAttempts { get; init; }

    public int MaxBackoffSeconds { get; init; }
}

public sealed class MonitoringRetentionConfigDto
{
    public int HistoryDays { get; init; }

    public int MaxHistoryPerTarget { get; init; }

    public int PurgeBatchSize { get; init; }

    public int KeepLastOutagesPerTarget { get; init; }

    public string? ScheduleUtc { get; init; }
}

public sealed class MonitoringAlertsConfigDto
{
    public int DefaultCooldownSeconds { get; init; }
}
