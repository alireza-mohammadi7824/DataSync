using System;
using Volo.Abp.Domain.Entities;

namespace Monitoring.Targets;

public class ServiceStatusHistory : Entity<Guid>
{
    public Guid TargetId { get; private set; }
    public ServiceStatus FromStatus { get; private set; }
    public ServiceStatus ToStatus { get; private set; }
    public DateTime ChangedAt { get; private set; }
    public string TriggerSource { get; private set; } = null!;
    public int? ResponseTimeMs { get; private set; }
    public string? ErrorSummary { get; private set; }

    private ServiceStatusHistory()
    {
    }

    public ServiceStatusHistory(
        Guid id,
        Guid targetId,
        ServiceStatus fromStatus,
        ServiceStatus toStatus,
        DateTime changedAt,
        string triggerSource,
        int? responseTimeMs,
        string? errorSummary)
        : base(id)
    {
        TargetId = targetId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ChangedAt = changedAt;
        SetTriggerSource(triggerSource);
        SetResponseDetails(responseTimeMs, errorSummary);
    }

    public void SetTriggerSource(string triggerSource)
    {
        MonitoringHistoryConsts.ValidateTriggerSource(triggerSource);
        TriggerSource = triggerSource;
    }

    public void SetResponseDetails(int? responseTimeMs, string? errorSummary)
    {
        ResponseTimeMs = responseTimeMs;
        MonitoringHistoryConsts.ValidateErrorSummary(errorSummary);
        ErrorSummary = errorSummary;
    }
}
