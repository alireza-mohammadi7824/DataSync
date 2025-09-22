using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Monitoring.Permissions;
using Monitoring.Targets;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace Monitoring.Dashboard;

[Authorize(MonitoringPermissions.Dashboard.View)]
public class DashboardAppService : ApplicationService, IDashboardAppService
{
    private readonly IRepository<MonitoringTarget, Guid> _targetRepository;
    private readonly IRepository<ServiceStatusHistory, Guid> _historyRepository;
    private readonly IRepository<OutageWindow, Guid> _outageRepository;
    private readonly IAsyncQueryableExecuter _asyncExecuter;

    public DashboardAppService(
        IRepository<MonitoringTarget, Guid> targetRepository,
        IRepository<ServiceStatusHistory, Guid> historyRepository,
        IRepository<OutageWindow, Guid> outageRepository,
        IAsyncQueryableExecuter asyncExecuter)
    {
        _targetRepository = targetRepository;
        _historyRepository = historyRepository;
        _outageRepository = outageRepository;
        _asyncExecuter = asyncExecuter;
    }

    public virtual async Task<DashboardSummaryDto> GetSummaryAsync()
    {
        var nowUtc = Clock.Normalize(Clock.Now);

        var window24Start = nowUtc.AddHours(-24);
        var window7Start = nowUtc.AddDays(-7);
        var window30Start = nowUtc.AddDays(-30);

        var targetQuery = await _targetRepository.GetQueryableAsync();
        var targetSnapshots = await _asyncExecuter.ToListAsync(
            targetQuery.Select(t => new { t.Id, t.CurrentStatus }));

        var totalTargets = targetSnapshots.Count;

        var summary = new DashboardSummaryDto
        {
            GeneratedAtUtc = nowUtc,
            OnlineCount = targetSnapshots.Count(t => t.CurrentStatus == ServiceStatus.Online),
            OfflineCount = targetSnapshots.Count(t => t.CurrentStatus == ServiceStatus.Offline),
            CheckingCount = targetSnapshots.Count(t => t.CurrentStatus == ServiceStatus.Checking),
            Uptime24h = 100d,
            Uptime7d = 100d,
            Uptime30d = 100d,
            Mttr30d = 0d,
            Mtbf30d = 0d
        };

        if (totalTargets == 0)
        {
            return summary;
        }

        var outageQuery = await _outageRepository.GetQueryableAsync();
        var outages = await _asyncExecuter.ToListAsync(
            outageQuery
                .Where(o => o.StartedAt <= nowUtc && (o.EndedAt == null || o.EndedAt >= window30Start)));

        var downtime24 = SumDowntime(outages, window24Start, nowUtc, nowUtc);
        var downtime7 = SumDowntime(outages, window7Start, nowUtc, nowUtc);
        var downtime30 = SumDowntime(outages, window30Start, nowUtc, nowUtc);

        var window24TotalSeconds = Math.Max(0d, (nowUtc - window24Start).TotalSeconds * totalTargets);
        var window7TotalSeconds = Math.Max(0d, (nowUtc - window7Start).TotalSeconds * totalTargets);
        var window30TotalSeconds = Math.Max(0d, (nowUtc - window30Start).TotalSeconds * totalTargets);

        summary.Uptime24h = DashboardMetricsHelper.UptimePercent(TimeSpan.FromSeconds(window24TotalSeconds), TimeSpan.FromSeconds(downtime24));
        summary.Uptime7d = DashboardMetricsHelper.UptimePercent(TimeSpan.FromSeconds(window7TotalSeconds), TimeSpan.FromSeconds(downtime7));
        summary.Uptime30d = DashboardMetricsHelper.UptimePercent(TimeSpan.FromSeconds(window30TotalSeconds), TimeSpan.FromSeconds(downtime30));

        var closedOutages = outages
            .Where(o => o.EndedAt.HasValue && o.EndedAt.Value >= window30Start && o.EndedAt.Value <= nowUtc)
            .OrderBy(o => o.StartedAt)
            .ToList();

        if (closedOutages.Count > 0)
        {
            var mttrHours = closedOutages
                .Select(o => (o.TotalDurationSec ?? (int)Math.Max(0, (o.EndedAt!.Value - o.StartedAt).TotalSeconds)) / 3600d)
                .Where(duration => duration > 0d)
                .ToList();

            if (mttrHours.Count > 0)
            {
                summary.Mttr30d = Math.Round(mttrHours.Average(), 2);
            }
        }

        var mtbfHours = outages
            .Where(o => o.StartedAt >= window30Start && o.StartedAt <= nowUtc)
            .GroupBy(o => o.TargetId)
            .SelectMany(group => CalculateIntervals(group.OrderBy(o => o.StartedAt).ToList()))
            .ToList();

        if (mtbfHours.Count > 0)
        {
            summary.Mtbf30d = Math.Round(mtbfHours.Average(), 2);
        }

        return summary;
    }

    public virtual async Task<TargetDashboardListDto> GetTargetsAsync(TargetDashboardListInput input)
    {
        input ??= new TargetDashboardListInput();

        var nowUtc = Clock.Normalize(Clock.Now);
        var window24Start = nowUtc.AddHours(-24);
        var window7Start = nowUtc.AddDays(-7);
        var window30Start = nowUtc.AddDays(-30);

        var query = await _targetRepository.GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(input.ServiceType) && Enum.TryParse<ServiceType>(input.ServiceType, true, out var serviceType))
        {
            query = query.Where(t => t.Type == serviceType);
        }

        if (input.Status.HasValue)
        {
            var status = MapToServiceStatus(input.Status.Value);
            query = query.Where(t => t.CurrentStatus == status);
        }

        var totalCount = await _asyncExecuter.CountAsync(query);

        var skipCount = input.SkipCount < 0 ? 0 : input.SkipCount;
        var maxResultCount = input.MaxResultCount > 0 ? Math.Min(input.MaxResultCount, 100) : 20;

        var sortedQuery = ApplySorting(query, input.Sorting);

        var targetData = await _asyncExecuter.ToListAsync(
            sortedQuery
                .Skip(skipCount)
                .Take(maxResultCount)
                .Select(t => new
                {
                    t.Id,
                    t.Name,
                    t.Type,
                    t.CurrentStatus
                }));

        if (targetData.Count == 0)
        {
            return new TargetDashboardListDto(totalCount, Array.Empty<TargetDashboardItemDto>());
        }

        var targetIds = targetData.Select(t => t.Id).ToList();

        var outageQuery = await _outageRepository.GetQueryableAsync();
        var outages = await _asyncExecuter.ToListAsync(
            outageQuery
                .Where(o => targetIds.Contains(o.TargetId))
                .Where(o => o.StartedAt <= nowUtc && (o.EndedAt == null || o.EndedAt >= window30Start)));

        var outagesByTarget = outages
            .GroupBy(o => o.TargetId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(o => o.StartedAt).ToList());

        var historyQuery = await _historyRepository.GetQueryableAsync();
        var historyEntries = await _asyncExecuter.ToListAsync(
            historyQuery
                .Where(h => targetIds.Contains(h.TargetId))
                .Where(h => h.ChangedAt >= window30Start)
                .OrderByDescending(h => h.ChangedAt)
                .Select(h => new { h.TargetId, h.ToStatus, h.ChangedAt }));

        var lastOnlineLookup = historyEntries
            .Where(entry => entry.ToStatus == ServiceStatus.Online)
            .GroupBy(entry => entry.TargetId)
            .ToDictionary(group => group.Key, group => group.Max(x => x.ChangedAt));

        var window24 = nowUtc - window24Start;
        var window7 = nowUtc - window7Start;
        var window30 = nowUtc - window30Start;

        var items = new List<TargetDashboardItemDto>(targetData.Count);

        foreach (var target in targetData)
        {
            outagesByTarget.TryGetValue(target.Id, out var targetOutages);
            targetOutages ??= new List<OutageWindow>();

            var downtime24 = SumDowntime(targetOutages, window24Start, nowUtc, nowUtc);
            var downtime7 = SumDowntime(targetOutages, window7Start, nowUtc, nowUtc);
            var downtime30 = SumDowntime(targetOutages, window30Start, nowUtc, nowUtc);

            var lastOutage = targetOutages.FirstOrDefault();
            DateTime? lastOutageEndUtc = lastOutage?.EndedAt;

            if (lastOutageEndUtc == null && lastOnlineLookup.TryGetValue(target.Id, out var lastOnline))
            {
                lastOutageEndUtc = lastOnline;
            }

            items.Add(new TargetDashboardItemDto
            {
                Id = target.Id,
                Name = target.Name,
                ServiceType = target.Type.ToString(),
                CurrentStatus = MapToDtoStatus(target.CurrentStatus),
                Uptime24h = DashboardMetricsHelper.UptimePercent(window24, TimeSpan.FromSeconds(downtime24)),
                Uptime7d = DashboardMetricsHelper.UptimePercent(window7, TimeSpan.FromSeconds(downtime7)),
                Uptime30d = DashboardMetricsHelper.UptimePercent(window30, TimeSpan.FromSeconds(downtime30)),
                LastOutageStartUtc = lastOutage?.StartedAt,
                LastOutageEndUtc = lastOutageEndUtc
            });
        }

        return new TargetDashboardListDto(totalCount, items);
    }

    private static IQueryable<MonitoringTarget> ApplySorting(IQueryable<MonitoringTarget> query, string? sorting)
    {
        if (string.IsNullOrWhiteSpace(sorting))
        {
            return query.OrderBy(target => target.Name);
        }

        var normalized = sorting.Trim().ToLowerInvariant();

        return normalized switch
        {
            "name desc" or "name descending" => query.OrderByDescending(target => target.Name),
            "servicetype desc" or "servicetype descending" => query.OrderByDescending(target => target.Type),
            "servicetype" or "servicetype asc" => query.OrderBy(target => target.Type),
            "status desc" or "status descending" => query.OrderByDescending(target => target.CurrentStatus),
            "status" or "status asc" => query.OrderBy(target => target.CurrentStatus),
            _ => query.OrderBy(target => target.Name)
        };
    }

    private static ServiceStatus MapToServiceStatus(TargetStatusDto status)
    {
        return status switch
        {
            TargetStatusDto.Online => ServiceStatus.Online,
            TargetStatusDto.Offline => ServiceStatus.Offline,
            _ => ServiceStatus.Checking
        };
    }

    private static TargetStatusDto MapToDtoStatus(ServiceStatus status)
    {
        return status switch
        {
            ServiceStatus.Online => TargetStatusDto.Online,
            ServiceStatus.Offline => TargetStatusDto.Offline,
            _ => TargetStatusDto.Checking
        };
    }

    private static IEnumerable<double> CalculateIntervals(IReadOnlyList<OutageWindow> outages)
    {
        for (var i = 1; i < outages.Count; i++)
        {
            var previous = outages[i - 1];
            var current = outages[i];
            var interval = (current.StartedAt - previous.StartedAt).TotalHours;
            if (interval > 0d)
            {
                yield return interval;
            }
        }
    }

    private static double SumDowntime(IEnumerable<OutageWindow> outages, DateTime windowStart, DateTime windowEnd, DateTime nowUtc)
    {
        double totalSeconds = 0d;
        foreach (var outage in outages)
        {
            totalSeconds += DashboardMetricsHelper.OverlapUtc(outage.StartedAt, outage.EndedAt, windowStart, windowEnd, nowUtc).TotalSeconds;
        }

        return totalSeconds;
    }
}
