using System;
using Volo.Abp.Application.Dtos;

namespace Monitoring.Targets;

public class MonitoringTargetDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; } = string.Empty;

    public ServiceType Type { get; set; }

    public string Endpoint { get; set; } = string.Empty;

    public string? SettingsJson { get; set; }

    public int CheckIntervalSeconds { get; set; }

    public int TimeoutSeconds { get; set; }

    public int MaxRetryAttempts { get; set; }

    public int RetryDelaySeconds { get; set; }

    public string? Category { get; set; }

    public bool IsActive { get; set; }

    public ServiceStatus CurrentStatus { get; set; }

    public DateTime? LastCheckedAt { get; set; }

    public DateTime? LastStatusChangeAt { get; set; }

    public DateTime NextDueAt { get; set; }

    public int ConsecutiveFailures { get; set; }

    public DateTime? FirstDownAt { get; set; }

    public DateTime? LastUpAt { get; set; }
}
