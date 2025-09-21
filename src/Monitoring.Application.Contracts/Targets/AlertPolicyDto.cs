using System;
using Volo.Abp.Application.Dtos;

namespace Monitoring.Targets;

public class AlertPolicyDto : EntityDto<Guid>
{
    public Guid TargetId { get; set; }

    public bool Enabled { get; set; } = true;

    public int NotifyAfterFailures { get; set; } = 1;

    public int RepeatMinutes { get; set; } = 60;

    public int RecoverQuietMinutes { get; set; } = 10;

    public string? ChannelsJson { get; set; }

    public bool SuppressDuringMaintenance { get; set; } = true;

    public bool IsInherited { get; set; }
}
