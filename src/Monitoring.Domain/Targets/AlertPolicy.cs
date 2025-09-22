using System;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Monitoring.Targets;

public class AlertPolicy : FullAuditedEntity<Guid>
{
    public Guid TargetId { get; private set; }

    public bool Enabled { get; private set; }

    public int NotifyAfterFailures { get; private set; }

    public int RepeatMinutes { get; private set; }

    public int RecoverQuietMinutes { get; private set; }

    public string? ChannelsJson { get; private set; }

    public bool SuppressDuringMaintenance { get; private set; }

    protected AlertPolicy()
    {
    }

    public AlertPolicy(
        Guid id,
        Guid targetId,
        bool enabled,
        int notifyAfterFailures,
        int repeatMinutes,
        int recoverQuietMinutes,
        string? channelsJson,
        bool suppressDuringMaintenance)
        : base(id)
    {
        TargetId = targetId;
        Update(enabled, notifyAfterFailures, repeatMinutes, recoverQuietMinutes, channelsJson, suppressDuringMaintenance);
    }

    public void Update(
        bool enabled,
        int notifyAfterFailures,
        int repeatMinutes,
        int recoverQuietMinutes,
        string? channelsJson,
        bool suppressDuringMaintenance)
    {
        Enabled = enabled;
        SetNotifyAfterFailures(notifyAfterFailures);
        SetRepeatMinutes(repeatMinutes);
        SetRecoverQuietMinutes(recoverQuietMinutes);
        SetChannelsJson(channelsJson);
        SuppressDuringMaintenance = suppressDuringMaintenance;
    }

    private void SetNotifyAfterFailures(int value)
    {
        if (value < 1)
        {
            throw new BusinessException("Monitoring:InvalidNotifyAfterFailures")
                .WithData("Minimum", 1);
        }

        NotifyAfterFailures = value;
    }

    private void SetRepeatMinutes(int value)
    {
        if (value < 1)
        {
            throw new BusinessException("Monitoring:InvalidRepeatMinutes")
                .WithData("Minimum", 1);
        }

        RepeatMinutes = value;
    }

    private void SetRecoverQuietMinutes(int value)
    {
        if (value < 0)
        {
            throw new BusinessException("Monitoring:InvalidRecoverQuietMinutes")
                .WithData("Minimum", 0);
        }

        RecoverQuietMinutes = value;
    }

    private void SetChannelsJson(string? value)
    {
        if (!value.IsNullOrWhiteSpace())
        {
            Check.Length(value, nameof(ChannelsJson), AlertPolicyConsts.ChannelsJsonMaxLength, 0);

            try
            {
                JsonDocument.Parse(value);
            }
            catch (JsonException ex)
            {
                throw new BusinessException("Monitoring:InvalidAlertChannels", innerException: ex);
            }
        }

        ChannelsJson = value;
    }
}
