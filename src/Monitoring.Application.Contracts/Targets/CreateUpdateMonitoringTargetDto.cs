using System.ComponentModel.DataAnnotations;

namespace Monitoring.Targets;

public class CreateUpdateMonitoringTargetDto
{
    [Required]
    [StringLength(128, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public ServiceType Type { get; set; } = ServiceType.Website;

    [Required]
    [StringLength(512)]
    public string Endpoint { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? SettingsJson { get; set; }

    [Range(10, int.MaxValue)]
    public int CheckIntervalSeconds { get; set; } = 300;

    [Range(1, int.MaxValue)]
    public int TimeoutSeconds { get; set; } = 30;

    [Range(0, int.MaxValue)]
    public int MaxRetryAttempts { get; set; } = 3;

    [Range(1, int.MaxValue)]
    public int RetryDelaySeconds { get; set; } = 30;

    [StringLength(100)]
    public string? Category { get; set; }

    public bool IsActive { get; set; } = true;
}
