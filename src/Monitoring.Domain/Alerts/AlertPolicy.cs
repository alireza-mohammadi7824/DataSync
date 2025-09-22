using System;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;

namespace Monitoring.Alerts;

public class AlertPolicy : Entity<Guid>, IMayHaveCreator, IHasCreationTime
{
    public Guid? TargetId { get; private set; }

    public bool OnDown { get; private set; } = true;

    public bool OnUp { get; private set; } = true;

    public int MinDownDurationSeconds { get; private set; }

    public int CooldownSeconds { get; private set; } = 300;

    public string Emails { get; private set; } = string.Empty;

    public string? WebhookUrl { get; private set; }

    public DateTime CreationTime { get; set; }

    public Guid? CreatorId { get; set; }

    protected AlertPolicy()
    {
    }

    public AlertPolicy(
        Guid id,
        Guid? targetId,
        bool onDown,
        bool onUp,
        int minDownSec,
        int cooldownSec,
        string emails,
        string? webhookUrl)
    {
        Id = id;
        SetTargetId(targetId);
        Update(onDown, onUp, minDownSec, cooldownSec, emails, webhookUrl);
        CreationTime = DateTime.UtcNow;
    }

    public void Update(
        bool onDown,
        bool onUp,
        int minDownSec,
        int cooldownSec,
        string emails,
        string? webhookUrl)
    {
        OnDown = onDown;
        OnUp = onUp;
        MinDownDurationSeconds = Math.Max(0, minDownSec);
        CooldownSeconds = Math.Max(0, cooldownSec);
        Emails = emails?.Trim() ?? string.Empty;
        WebhookUrl = string.IsNullOrWhiteSpace(webhookUrl) ? null : webhookUrl.Trim();
    }

    public void SetTargetId(Guid? targetId)
    {
        TargetId = targetId;
    }
}
