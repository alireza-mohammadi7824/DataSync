using System;
using Volo.Abp.Application.Dtos;

namespace Monitoring.Targets;

public class OutageWindowDto : EntityDto<Guid>
{
    public Guid TargetId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int FailureCount { get; set; }
    public int? TotalDurationSec { get; set; }
}
