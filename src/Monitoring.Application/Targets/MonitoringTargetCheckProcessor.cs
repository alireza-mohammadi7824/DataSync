using System;
using Monitoring.HealthChecks;
using Volo.Abp.Data;

namespace Monitoring.Targets;

internal static class MonitoringTargetCheckProcessor
{
    private const string LastResponseTimePropertyName = "Monitoring:LastResponseTimeMs";
    private const string LastErrorSummaryPropertyName = "Monitoring:LastErrorSummary";
    private const string LastTriggerSourcePropertyName = "Monitoring:LastTriggerSource";

    public static void ApplyResult(MonitoringTarget target, HealthCheckResult result, DateTime timestamp)
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
        }
    }
}
