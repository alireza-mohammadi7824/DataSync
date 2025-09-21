using System;
using Volo.Abp.Application.Dtos;

namespace Monitoring.Targets;

public class MaintenanceWindowDto : EntityDto<Guid>
{
    public Guid? TargetId { get; set; }

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    public string? Reason { get; set; }
}
