using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Monitoring.Targets;

public class MaintenanceWindow : FullAuditedEntity<Guid>
{
    public Guid? TargetId { get; private set; }

    public bool IsGlobal { get; private set; }

    public DateTime StartUtc { get; private set; }

    public DateTime EndUtc { get; private set; }

    public string? Reason { get; private set; }

    protected MaintenanceWindow()
    {
    }

    public MaintenanceWindow(Guid id, Guid? targetId, DateTime startUtc, DateTime endUtc, string? reason)
        : base(id)
    {
        SetScope(targetId);
        UpdateWindow(startUtc, endUtc, reason);
    }

    public void SetScope(Guid? targetId)
    {
        TargetId = targetId;
        IsGlobal = !targetId.HasValue;
    }

    public void UpdateWindow(DateTime startUtc, DateTime endUtc, string? reason)
    {
        startUtc = NormalizeUtc(startUtc, nameof(StartUtc));
        endUtc = NormalizeUtc(endUtc, nameof(EndUtc));

        if (endUtc <= startUtc)
        {
            throw new BusinessException("Monitoring:MaintenanceWindowInvalidRange");
        }

        StartUtc = startUtc;
        EndUtc = endUtc;
        SetReason(reason);
    }

    public void SetReason(string? reason)
    {
        if (!reason.IsNullOrWhiteSpace())
        {
            Check.Length(reason, nameof(Reason), MaintenanceWindowConsts.ReasonMaxLength, 0);
        }

        Reason = reason;
    }

    private static DateTime NormalizeUtc(DateTime value, string field)
    {
        if (value.Kind == DateTimeKind.Unspecified)
        {
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        if (value.Kind != DateTimeKind.Utc)
        {
            throw new BusinessException("Monitoring:MaintenanceWindowRequiresUtc")
                .WithData("Field", field);
        }

        return value;
    }
}
