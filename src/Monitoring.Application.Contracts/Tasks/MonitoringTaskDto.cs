using System;
using Volo.Abp.Application.Dtos;

namespace Monitoring.Tasks;

public class MonitoringTaskDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; } = string.Empty;

    public string TargetUrl { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public int CheckIntervalInSeconds { get; set; }

    public DateTime? LastExecutionTime { get; set; }

    public string? AuthenticationSecretRef { get; set; }

    public int ConsecutiveFailureCount { get; set; }
}
