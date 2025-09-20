using System;
using System.ComponentModel.DataAnnotations;

namespace Monitoring.Targets;

public class CreateUpdateMonitoringTargetDto
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public ServiceType Type { get; set; } = ServiceType.Website;

    [Required]
    [StringLength(512)]
    public string Endpoint { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? SettingsJson { get; set; }

    [Range(1, int.MaxValue)]
    public int CheckIntervalSeconds { get; set; } = 300;

    [Range(1, int.MaxValue)]
    public int TimeoutSeconds { get; set; } = 30;

    [Range(0, int.MaxValue)]
    public int MaxRetryAttempts { get; set; } = 3;

    [Range(0, int.MaxValue)]
    public int RetryDelaySeconds { get; set; } = 30;

    [StringLength(100)]
    public string? Category { get; set; }

    public bool IsActive { get; set; } = true;

    public ServiceStatus CurrentStatus { get; set; } = ServiceStatus.Checking;

    public DateTime? LastCheckedAt { get; set; }

    public DateTime? LastStatusChangeAt { get; set; }

    [Required]
    public DateTime NextDueAt { get; set; }

    [Range(0, int.MaxValue)]
    public int ConsecutiveFailures { get; set; } = 0;

    public DateTime? FirstDownAt { get; set; }

    public DateTime? LastUpAt { get; set; }
}
