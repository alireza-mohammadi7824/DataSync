using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Monitoring.Dashboard;
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
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 200;

    private readonly IReadOnlyRepository<MonitoringTarget, Guid> _targetRepository;
    private readonly IReadOnlyRepository<ServiceStatusHistory, Guid> _historyRepository;
    private readonly IReadOnlyRepository<OutageWindow, Guid> _outageRepository;
    private readonly IAsyncQueryableExecuter _asyncExecuter;

    public DashboardAppService(
        IReadOnlyRepository<MonitoringTarget, Guid> targetRepository,
        IReadOnlyRepository<ServiceStatusHistory, Guid> historyRepository,
        IReadOnlyRepository<OutageWindow, Guid> outageRepository,
        IAsyncQueryableExecuter asyncExecuter)
    {
        _targetRepository = targetRepository;
        _historyRepository = historyRepository;
        _outageRepository = outageRepository;
        _asyncExecuter = asyncExecuter;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        await AuthorizationService.CheckAsync(MonitoringPermissions.Dashboard.View);

        _ = _historyRepository;

        var now = Clock.Now.ToUniversalTime();
        var window24Start = now.AddHours(-24);
        var window7Start = now.AddDays(-7);
        var window30Start = now.AddDays(-30);

        var targetQuery = await _targetRepository.GetQueryableAsync();
        var targetSnapshots = await _asyncExecuter.ToListAsync(
            targetQuery.Select(t => new
            {
                t.Id,
                t.IsActive,
                t.CurrentStatus
            }),
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        var summary = new DashboardSummaryDto
        {
            GeneratedAtUtc = now,
            OnlineCount = targetSnapshots.Count(t => t.CurrentStatus == ServiceStatus.Online),
            OfflineCount = targetSnapshots.Count(t => t.CurrentStatus == ServiceStatus.Offline),
            CheckingCount = targetSnapshots.Count(t => t.CurrentStatus == ServiceStatus.Checking)
        };

        var activeTargetIds = targetSnapshots
            .Where(t => t.IsActive)
            .Select(t => t.Id)
            .ToList();

        var activeCount = activeTargetIds.Count;
        if (activeCount == 0)
        {
            summary.Uptime24h = 100d;
            summary.Uptime7d = 100d;
            summary.Uptime30d = 100d;
            summary.Mttr30d = 0d;
            summary.Mtbf30d = 0d;
            return summary;
        }

        var outageQuery = await _outageRepository.GetQueryableAsync();
        var outages = await _asyncExecuter.ToListAsync(
            outageQuery
                .Where(o => activeTargetIds.Contains(o.TargetId))
                .Where(o => o.StartedAt <= now && (o.EndedAt == null || o.EndedAt >= window30Start)),
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        var downtime24 = SumDowntime(outages, window24Start, now, now);
        var downtime7 = SumDowntime(outages, window7Start, now, now);
        var downtime30 = SumDowntime(outages, window30Start, now, now);

        var window24 = TimeSpan.FromHours(24);
        var window7 = TimeSpan.FromDays(7);
        var window30 = TimeSpan.FromDays(30);

        summary.Uptime24h = DashboardMetricsHelper.UptimePercent(
            ScaleWindow(window24, activeCount),
            downtime24);
        summary.Uptime7d = DashboardMetricsHelper.UptimePercent(
            ScaleWindow(window7, activeCount),
            downtime7);
        summary.Uptime30d = DashboardMetricsHelper.UptimePercent(
            ScaleWindow(window30, activeCount),
            downtime30);

        var closedOutages = outages
            .Where(o => o.EndedAt.HasValue && o.EndedAt.Value >= window30Start)
            .ToList();

        if (closedOutages.Count > 0)
        {
            summary.Mttr30d = Math.Round(
                closedOutages.Average(o => Math.Max(0d, (o.EndedAt!.Value - o.StartedAt).TotalMinutes)),
                2);
        }

        var mtbfCandidates = outages
            .Where(o => (o.EndedAt ?? now) >= window30Start)
            .OrderBy(o => o.StartedAt)
            .ToList();

        if (mtbfCandidates.Count > 1)
        {
            double totalGap = 0d;
            var gapCount = 0;
            for (var i = 1; i < mtbfCandidates.Count; i++)
            {
                var gap = (mtbfCandidates[i].StartedAt - mtbfCandidates[i - 1].StartedAt).TotalMinutes;
                if (gap > 0)
                {
                    totalGap += gap;
                    gapCount++;
                }
            }

            if (gapCount > 0)
            {
                summary.Mtbf30d = Math.Round(totalGap / gapCount, 2);
            }
        }

        return summary;
    }

    public async Task<TargetDashboardListDto> GetTargetsAsync(
        TargetDashboardListInput input,
        CancellationToken cancellationToken = default)
    {
        await AuthorizationService.CheckAsync(MonitoringPermissions.Dashboard.View);

        var now = Clock.Now.ToUniversalTime();
        var window24Start = now.AddHours(-24);
        var window7Start = now.AddDays(-7);
        var window30Start = now.AddDays(-30);

        var query = await _targetRepository.GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(input.ServiceType))
        {
            if (Enum.TryParse<ServiceType>(input.ServiceType, true, out var serviceType))
            {
                query = query.Where(t => t.Type == serviceType);
            }
            else
            {
                query = query.Where(_ => false);
            }
        }

        if (input.Status.HasValue)
        {
            var status = MapStatus(input.Status.Value);
            query = query.Where(t => t.CurrentStatus == status);
        }

        query = ApplySorting(query, input.Sorting);

        var totalCount = await _asyncExecuter.CountAsync(query, cancellationToken);

        var skipCount = input.SkipCount < 0 ? 0 : input.SkipCount;
        var maxResultCount = input.MaxResultCount <= 0 ? DefaultPageSize : Math.Min(input.MaxResultCount, MaxPageSize);

        var items = await _asyncExecuter.ToListAsync(
            query
                .Skip(skipCount)
                .Take(maxResultCount)
                .Select(t => new
                {
                    t.Id,
                    t.Name,
                    t.Type,
                    t.CurrentStatus,
                    t.IsActive
                }),
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        var targetIds = items.Select(i => i.Id).ToList();
        var outagesByTarget = new Dictionary<Guid, List<OutageWindow>>();

        if (targetIds.Count > 0)
        {
            var outageQuery = await _outageRepository.GetQueryableAsync();
            var outages = await _asyncExecuter.ToListAsync(
                outageQuery
                    .Where(o => targetIds.Contains(o.TargetId))
                    .Where(o => o.StartedAt <= now && (o.EndedAt == null || o.EndedAt >= window30Start)),
                cancellationToken);

            foreach (var outage in outages)
            {
                if (!outagesByTarget.TryGetValue(outage.TargetId, out var list))
                {
                    list = new List<OutageWindow>();
                    outagesByTarget[outage.TargetId] = list;
                }

                list.Add(outage);
            }
        }

        var window24 = TimeSpan.FromHours(24);
        var window7 = TimeSpan.FromDays(7);
        var window30 = TimeSpan.FromDays(30);

        var resultItems = new List<TargetDashboardItemDto>(items.Count);
        foreach (var item in items)
        {
            outagesByTarget.TryGetValue(item.Id, out var targetOutages);
            targetOutages ??= new List<OutageWindow>();

            var downtime24 = SumDowntime(targetOutages, window24Start, now, now);
            var downtime7 = SumDowntime(targetOutages, window7Start, now, now);
            var downtime30 = SumDowntime(targetOutages, window30Start, now, now);

            var lastOutage = targetOutages
                .OrderByDescending(o => o.StartedAt)
                .FirstOrDefault();

            resultItems.Add(new TargetDashboardItemDto
            {
                Id = item.Id,
                Name = item.Name,
                ServiceType = item.Type.ToString(),
                CurrentStatus = MapStatus(item.CurrentStatus),
                Uptime24h = DashboardMetricsHelper.UptimePercent(window24, downtime24),
                Uptime7d = DashboardMetricsHelper.UptimePercent(window7, downtime7),
                Uptime30d = DashboardMetricsHelper.UptimePercent(window30, downtime30),
                LastOutageStartUtc = lastOutage?.StartedAt,
                LastOutageEndUtc = lastOutage?.EndedAt
            });
        }

        return new TargetDashboardListDto(totalCount, resultItems);
    }

    private static TimeSpan SumDowntime(IEnumerable<OutageWindow> outages, DateTime windowStart, DateTime windowEnd, DateTime nowUtc)
    {
        var total = TimeSpan.Zero;
        foreach (var outage in outages)
        {
            total += DashboardMetricsHelper.OverlapUtc(outage.StartedAt, outage.EndedAt, windowStart, windowEnd, nowUtc);
        }

        return total;
    }

    private static TimeSpan ScaleWindow(TimeSpan window, int count)
    {
        if (count <= 0)
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.FromTicks(window.Ticks * (long)count);
    }

    private static ServiceStatus MapStatus(TargetStatusDto status)
    {
        return status switch
        {
            TargetStatusDto.Online => ServiceStatus.Online,
            TargetStatusDto.Offline => ServiceStatus.Offline,
            TargetStatusDto.Checking => ServiceStatus.Checking,
            _ => ServiceStatus.Checking
        };
    }

    private static TargetStatusDto MapStatus(ServiceStatus status)
    {
        return status switch
        {
            ServiceStatus.Online => TargetStatusDto.Online,
            ServiceStatus.Offline => TargetStatusDto.Offline,
            ServiceStatus.Checking => TargetStatusDto.Checking,
            _ => TargetStatusDto.Checking
        };
    }

    private static IQueryable<MonitoringTarget> ApplySorting(IQueryable<MonitoringTarget> query, string? sorting)
    {
        if (string.IsNullOrWhiteSpace(sorting))
        {
            return query.OrderBy(t => t.Name);
        }

        var normalized = sorting.Trim().ToLowerInvariant();
        return normalized switch
        {
            "name desc" or "name descending" => query.OrderByDescending(t => t.Name),
            "servicetype" or "servicetype asc" => query.OrderBy(t => t.Type),
            "servicetype desc" or "servicetype descending" => query.OrderByDescending(t => t.Type),
            "status" or "status asc" => query.OrderBy(t => t.CurrentStatus),
            "status desc" or "status descending" => query.OrderByDescending(t => t.CurrentStatus),
            _ => query.OrderBy(t => t.Name)
        };
    }
}
