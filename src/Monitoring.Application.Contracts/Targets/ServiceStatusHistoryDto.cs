using System;
using Monitoring.Targets;
using Volo.Abp.Application.Dtos;

namespace Monitoring.Targets;

public class ServiceStatusHistoryDto : EntityDto<Guid>
{
    public Guid TargetId { get; set; }
    public ServiceStatus FromStatus { get; set; }
    public ServiceStatus ToStatus { get; set; }
    public DateTime ChangedAt { get; set; }
    public string TriggerSource { get; set; } = null!;
    public int? ResponseTimeMs { get; set; }
    public string? ErrorSummary { get; set; }
}
