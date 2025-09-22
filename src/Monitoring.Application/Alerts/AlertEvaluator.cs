using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Monitoring.Targets;
using Volo.Abp.Domain.Repositories;

namespace Monitoring.Alerts;

public sealed class AlertEvaluator
{
    private readonly IRepository<AlertPolicy, Guid> _policyRepository;

    public AlertEvaluator(
        IRepository<AlertPolicy, Guid> policyRepository)
    {
        _policyRepository = policyRepository;
    }

    public async Task<AlertEvaluationResult> EvaluateTransitionAsync(
        MonitoringTarget target,
        ServiceStatusHistory latestHistory,
        OutageWindow? outage,
        DateTime nowUtc,
        CancellationToken ct = default)
    {
        var eventType = DetermineEventType(latestHistory);
        if (eventType == null)
        {
            return new AlertEvaluationResult(false, outage);
        }

        var policies = await GetApplicablePoliciesAsync(target.Id, ct);
        if (policies.Count == 0)
        {
            return new AlertEvaluationResult(false, outage)
            {
                EventType = eventType.Value.ToString(),
                Summary = BuildSummary(target, latestHistory.ToStatus, eventType)
            };
        }

        var filtered = FilterPolicies(policies, eventType.Value, outage, nowUtc);
        if (filtered.Count == 0)
        {
            return new AlertEvaluationResult(false, outage)
            {
                EventType = eventType.Value.ToString(),
                Summary = BuildSummary(target, latestHistory.ToStatus, eventType)
            };
        }

        return new AlertEvaluationResult(true, outage)
        {
            EventType = eventType.Value.ToString(),
            Policies = filtered,
            Summary = BuildSummary(target, latestHistory.ToStatus, eventType)
        };
    }

    private async Task<IReadOnlyList<AlertPolicy>> GetApplicablePoliciesAsync(Guid targetId, CancellationToken ct)
    {
        var targetSpecific = await _policyRepository.GetListAsync(p => p.TargetId == targetId, cancellationToken: ct);
        if (targetSpecific.Count > 0)
        {
            return targetSpecific;
        }

        return await _policyRepository.GetListAsync(p => p.TargetId == null, cancellationToken: ct);
    }

    private static List<AlertPolicy> FilterPolicies(
        IReadOnlyList<AlertPolicy> policies,
        AlertEventType eventType,
        OutageWindow? outage,
        DateTime nowUtc)
    {
        var filtered = new List<AlertPolicy>();

        foreach (var policy in policies)
        {
            if (eventType == AlertEventType.Down && !policy.OnDown)
            {
                continue;
            }

            if (eventType == AlertEventType.Up && !policy.OnUp)
            {
                continue;
            }

            if (eventType == AlertEventType.Down && policy.MinDownDurationSeconds > 0)
            {
                if (outage == null)
                {
                    continue;
                }

                var duration = nowUtc - outage.StartedAt;
                if (duration < TimeSpan.FromSeconds(policy.MinDownDurationSeconds))
                {
                    continue;
                }
            }

            filtered.Add(policy);
        }

        return filtered;
    }

    private static AlertEventType? DetermineEventType(ServiceStatusHistory history)
    {
        if (history.FromStatus == ServiceStatus.Online && history.ToStatus == ServiceStatus.Offline)
        {
            return AlertEventType.Down;
        }

        if (history.FromStatus == ServiceStatus.Offline && history.ToStatus == ServiceStatus.Online)
        {
            return AlertEventType.Up;
        }

        return null;
    }

    private static string BuildSummary(MonitoringTarget target, ServiceStatus status, AlertEventType? eventType)
    {
        var statusText = status.ToString();
        var eventText = eventType?.ToString() ?? statusText;
        return $"{target.Name} {eventText}";
    }
}

public enum AlertEventType
{
    Down,
    Up
}
