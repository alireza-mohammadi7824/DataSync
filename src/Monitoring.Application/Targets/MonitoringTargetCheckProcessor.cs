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

    public static async Task ApplyResultAsync(
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

        await ManageOutageWindowAsync(
            target,
            previousStatus,
            newStatus,
            timestamp,
            outageRepository,
            guidGenerator,
            cancellationToken);
    }

    private static async Task ManageOutageWindowAsync(
        MonitoringTarget target,
        ServiceStatus previousStatus,
        ServiceStatus newStatus,
        DateTime timestamp,
        IRepository<OutageWindow, Guid> outageRepository,
        IGuidGenerator guidGenerator,
        CancellationToken cancellationToken)
    {
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
                }
                else
                {
                    outage.MarkAsStarted(timestamp, 1);
                    await outageRepository.UpdateAsync(outage, autoSave: true, cancellationToken);
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
                }
                else
                {
                    outage.IncrementFailure();
                    await outageRepository.UpdateAsync(outage, autoSave: true, cancellationToken);
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
            }
        }
    }
}
