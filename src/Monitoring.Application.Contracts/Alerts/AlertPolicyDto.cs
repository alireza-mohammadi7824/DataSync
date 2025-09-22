using System;
using Volo.Abp.Application.Dtos;

namespace Monitoring.Alerts;

public class AlertPolicyDto : EntityDto<Guid>
{
    public Guid? TargetId { get; set; }

    public bool OnDown { get; set; }

    public bool OnUp { get; set; }

    public int MinDownDurationSeconds { get; set; }

    public int CooldownSeconds { get; set; }

    public string[] Emails { get; set; } = Array.Empty<string>();

    public string? WebhookUrl { get; set; }
}
