using System;
using System.Threading;
using System.Threading.Tasks;
using Monitoring.HealthChecks;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace Monitoring.Targets;

internal static class MonitoringTargetCheckProcessor
{
    private const string LastResponseTimePropertyName = "Monitoring:LastResponseTimeMs";
    private const string LastErrorSummaryPropertyName = "Monitoring:LastErrorSummary";
    private const string LastTriggerSourcePropertyName = "Monitoring:LastTriggerSource";

    public static async Task<MonitoringCheckOutcome> ApplyResultAsync(
        MonitoringTarget target,
        HealthCheckResult result,
        DateTime timestamp,
        IRepository<ServiceStatusHistory, Guid> historyRepository,
        IRepository<OutageWindow, Guid> outageRepository,
        IGuidGenerator guidGenerator,
        CancellationToken cancellationToken = default)
    {
        var previousStatus = target.CurrentStatus;

        target.SetLastCheckedAt(timestamp);
        target.SetProperty(LastResponseTimePropertyName, result.ResponseTimeMs);
        target.SetProperty(LastErrorSummaryPropertyName, result.ErrorSummary);
        target.SetProperty(LastTriggerSourcePropertyName, result.TriggerSource);

        ServiceStatus newStatus;

        if (result.IsSuccess)
        {
            newStatus = ServiceStatus.Online;
            target.SetConsecutiveFailures(0);
            target.SetLastUpAt(timestamp);
            target.SetNextDueAt(timestamp.AddSeconds(target.CheckIntervalSeconds));
            target.SetFirstDownAt(null);
        }
        else
        {
            var failures = target.ConsecutiveFailures + 1;
            target.SetConsecutiveFailures(failures);

            if (failures < target.MaxRetryAttempts)
            {
                newStatus = ServiceStatus.Checking;
                var retryDelaySeconds = target.RetryDelaySeconds > 0 ? target.RetryDelaySeconds : 1;
                target.SetNextDueAt(timestamp.AddSeconds(retryDelaySeconds));
            }
            else
            {
                newStatus = ServiceStatus.Offline;
                target.SetNextDueAt(timestamp.AddSeconds(target.CheckIntervalSeconds));

                if (!target.FirstDownAt.HasValue)
                {
                    target.SetFirstDownAt(timestamp);
                }
            }
        }

        target.SetCurrentStatus(newStatus);

        if (newStatus != previousStatus)
        {
            target.SetLastStatusChangeAt(timestamp);

            var history = new ServiceStatusHistory(
                guidGenerator.Create(),
                target.Id,
                previousStatus,
                newStatus,
                timestamp,
                result.TriggerSource,
                result.ResponseTimeMs,
                result.ErrorSummary);

            await historyRepository.InsertAsync(history, autoSave: true, cancellationToken);
        }

        var outageTransition = await ManageOutageWindowAsync(
            target,
            previousStatus,
            newStatus,
            timestamp,
            outageRepository,
            guidGenerator,
            cancellationToken);

        return new MonitoringCheckOutcome(
            previousStatus,
            newStatus,
            outageTransition.ActiveOutage,
            outageTransition.ClosedOutage,
            outageTransition.OutageStarted,
            outageTransition.OutageClosed);
    }

    private static async Task<OutageTransitionResult> ManageOutageWindowAsync(
        MonitoringTarget target,
        ServiceStatus previousStatus,
        ServiceStatus newStatus,
        DateTime timestamp,
        IRepository<OutageWindow, Guid> outageRepository,
        IGuidGenerator guidGenerator,
        CancellationToken cancellationToken)
    {
        OutageWindow? activeOutage = null;
        OutageWindow? closedOutage = null;
        var outageStarted = false;
        var outageClosed = false;

        if (newStatus == ServiceStatus.Offline)
        {
            var outage = await outageRepository.FirstOrDefaultAsync(
                x => x.TargetId == target.Id && x.EndedAt == null,
                cancellationToken: cancellationToken);

            if (previousStatus != ServiceStatus.Offline)
            {
                if (outage == null)
                {
                    var newOutage = new OutageWindow(
                        guidGenerator.Create(),
                        target.Id,
                        timestamp,
                        failureCount: 1);

                    await outageRepository.InsertAsync(newOutage, autoSave: true, cancellationToken);
                    activeOutage = newOutage;
                    outageStarted = true;
                }
                else
                {
                    outage.MarkAsStarted(timestamp, 1);
                    await outageRepository.UpdateAsync(outage, autoSave: true, cancellationToken);
                    activeOutage = outage;
                    outageStarted = true;
                }
            }
            else
            {
                if (outage == null)
                {
                    var newOutage = new OutageWindow(
                        guidGenerator.Create(),
                        target.Id,
                        timestamp,
                        failureCount: 1);

                    await outageRepository.InsertAsync(newOutage, autoSave: true, cancellationToken);
                    activeOutage = newOutage;
                    outageStarted = true;
                }
                else
                {
                    outage.IncrementFailure();
                    await outageRepository.UpdateAsync(outage, autoSave: true, cancellationToken);
                    activeOutage = outage;
                }
            }
        }
        else if (previousStatus == ServiceStatus.Offline)
        {
            var outage = await outageRepository.FirstOrDefaultAsync(
                x => x.TargetId == target.Id && x.EndedAt == null,
                cancellationToken: cancellationToken);

            if (outage != null)
            {
                outage.Close(timestamp);
                await outageRepository.UpdateAsync(outage, autoSave: true, cancellationToken);
                closedOutage = outage;
                outageClosed = true;
            }
        }

        return new OutageTransitionResult(activeOutage, closedOutage, outageStarted, outageClosed);
    }
}

internal sealed class MonitoringCheckOutcome
{
    public MonitoringCheckOutcome(
        ServiceStatus previousStatus,
        ServiceStatus currentStatus,
        OutageWindow? activeOutage,
        OutageWindow? closedOutage,
        bool outageStarted,
        bool outageClosed)
    {
        PreviousStatus = previousStatus;
        CurrentStatus = currentStatus;
        ActiveOutage = activeOutage;
        ClosedOutage = closedOutage;
        OutageStarted = outageStarted;
        OutageClosed = outageClosed;
    }

    public ServiceStatus PreviousStatus { get; }
    public ServiceStatus CurrentStatus { get; }
    public OutageWindow? ActiveOutage { get; }
    public OutageWindow? ClosedOutage { get; }
    public bool OutageStarted { get; }
    public bool OutageClosed { get; }
}

internal sealed class OutageTransitionResult
{
    public OutageTransitionResult(
        OutageWindow? activeOutage,
        OutageWindow? closedOutage,
        bool outageStarted,
        bool outageClosed)
    {
        ActiveOutage = activeOutage;
        ClosedOutage = closedOutage;
        OutageStarted = outageStarted;
        OutageClosed = outageClosed;
    }

    public OutageWindow? ActiveOutage { get; }
    public OutageWindow? ClosedOutage { get; }
    public bool OutageStarted { get; }
    public bool OutageClosed { get; }
}
