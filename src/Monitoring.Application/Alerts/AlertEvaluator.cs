using System;
using Monitoring.Targets;

namespace Monitoring.Alerts;

internal static class AlertEvaluator
{
    public static AlertEvaluationResult Evaluate(AlertEvaluationInput input)
    {
        if (!input.Enabled)
        {
            return AlertEvaluationResult.None;
        }

        if (input.UnderMaintenance)
        {
            return AlertEvaluationResult.None;
        }

        if (input.CurrentStatus == ServiceStatus.Offline)
        {
            if (input.PreviousStatus != ServiceStatus.Offline)
            {
                if (input.ConsecutiveFailures >= input.NotifyAfterFailures)
                {
                    return new AlertEvaluationResult(AlertEventType.Down, input.ActiveOutage, shouldRecordAlert: true);
                }

                return AlertEvaluationResult.None;
            }

            if (input.ActiveOutage == null)
            {
                return AlertEvaluationResult.None;
            }

            if (!input.ActiveOutage.LastAlertAt.HasValue)
            {
                // This covers the case when retries delayed the first alert.
                if (input.ConsecutiveFailures >= input.NotifyAfterFailures)
                {
                    return new AlertEvaluationResult(AlertEventType.Down, input.ActiveOutage, shouldRecordAlert: true);
                }

                return AlertEvaluationResult.None;
            }

            var nextAllowed = input.ActiveOutage.LastAlertAt.Value.AddMinutes(input.RepeatMinutes);
            if (input.Timestamp >= nextAllowed)
            {
                return new AlertEvaluationResult(AlertEventType.StillDown, input.ActiveOutage, shouldRecordAlert: true);
            }

            return AlertEvaluationResult.None;
        }

        if (input.PreviousStatus == ServiceStatus.Offline && input.CurrentStatus == ServiceStatus.Online)
        {
            if (input.RecoverQuietMinutes <= 0)
            {
                return new AlertEvaluationResult(AlertEventType.Recovered, input.ClosedOutage, shouldRecordAlert: false);
            }

            var lastUp = input.PreviousLastUpAt;
            if (!lastUp.HasValue)
            {
                return new AlertEvaluationResult(AlertEventType.Recovered, input.ClosedOutage, shouldRecordAlert: false);
            }

            var quietThreshold = lastUp.Value.AddMinutes(input.RecoverQuietMinutes);
            if (input.Timestamp >= quietThreshold)
            {
                return new AlertEvaluationResult(AlertEventType.Recovered, input.ClosedOutage, shouldRecordAlert: false);
            }
        }

        return AlertEvaluationResult.None;
    }
}

internal readonly struct AlertEvaluationInput
{
    public AlertEvaluationInput(
        ServiceStatus previousStatus,
        ServiceStatus currentStatus,
        int consecutiveFailures,
        DateTime timestamp,
        int notifyAfterFailures,
        int repeatMinutes,
        int recoverQuietMinutes,
        bool enabled,
        bool underMaintenance,
        OutageWindow? activeOutage,
        OutageWindow? closedOutage,
        DateTime? previousLastUpAt)
    {
        PreviousStatus = previousStatus;
        CurrentStatus = currentStatus;
        ConsecutiveFailures = consecutiveFailures;
        Timestamp = timestamp;
        NotifyAfterFailures = notifyAfterFailures;
        RepeatMinutes = repeatMinutes;
        RecoverQuietMinutes = recoverQuietMinutes;
        Enabled = enabled;
        UnderMaintenance = underMaintenance;
        ActiveOutage = activeOutage;
        ClosedOutage = closedOutage;
        PreviousLastUpAt = previousLastUpAt;
    }

    public ServiceStatus PreviousStatus { get; }
    public ServiceStatus CurrentStatus { get; }
    public int ConsecutiveFailures { get; }
    public DateTime Timestamp { get; }
    public int NotifyAfterFailures { get; }
    public int RepeatMinutes { get; }
    public int RecoverQuietMinutes { get; }
    public bool Enabled { get; }
    public bool UnderMaintenance { get; }
    public OutageWindow? ActiveOutage { get; }
    public OutageWindow? ClosedOutage { get; }
    public DateTime? PreviousLastUpAt { get; }
}

internal readonly struct AlertEvaluationResult
{
    private AlertEvaluationResult(AlertEventType? eventType, OutageWindow? outage, bool shouldRecordAlert)
    {
        EventType = eventType;
        Outage = outage;
        ShouldRecordAlert = shouldRecordAlert;
    }

    public AlertEventType? EventType { get; }
    public OutageWindow? Outage { get; }
    public bool ShouldRecordAlert { get; }

    public static AlertEvaluationResult None => new(null, null, false);

    public AlertEvaluationResult(AlertEventType eventType, OutageWindow? outage, bool shouldRecordAlert)
        : this(eventType, outage, shouldRecordAlert)
    {
    }
}
