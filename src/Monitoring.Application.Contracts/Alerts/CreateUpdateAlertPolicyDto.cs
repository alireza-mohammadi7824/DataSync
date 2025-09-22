using System;
using System.ComponentModel.DataAnnotations;

namespace Monitoring.Alerts;

public class CreateUpdateAlertPolicyDto
{
    public Guid? TargetId { get; set; }

    public bool OnDown { get; set; } = true;

    public bool OnUp { get; set; } = true;

    [Range(0, int.MaxValue)]
    public int MinDownDurationSeconds { get; set; }

    [Range(0, int.MaxValue)]
    public int CooldownSeconds { get; set; } = 300;

    public string[] Emails { get; set; } = Array.Empty<string>();

    [Url]
    public string? WebhookUrl { get; set; }
}
