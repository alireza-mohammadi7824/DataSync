using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Monitoring.Dashboard;
using Monitoring.Options;
using Monitoring.Permissions;
using Monitoring.Targets;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;

namespace Monitoring.Dashboard;

public class DashboardAppService : ApplicationService, IDashboardAppService
{
    private const int DefaultIncidentPageSize = 50;

    private readonly IRepository<MonitoringTarget, Guid> _targetRepository;
    private readonly IRepository<OutageWindow, Guid> _outageRepository;
    private readonly MonitoringOptions _options;

    public DashboardAppService(
        IRepository<MonitoringTarget, Guid> targetRepository,
        IRepository<OutageWindow, Guid> outageRepository,
        IOptions<MonitoringOptions> options)
    {
        _targetRepository = targetRepository;
        _outageRepository = outageRepository;
        _options = options.Value;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(DateTime from, DateTime to, ServiceType? filterType = null)
    {
        await AuthorizationService.CheckAsync(MonitoringPermissions.Services.View);
        var (rangeStart, rangeEnd) = NormalizeRange(from, to);

        var targetQuery = await _targetRepository.GetQueryableAsync();
        if (filterType.HasValue)
        {
            targetQuery = targetQuery.Where(t => t.Type == filterType.Value);
        }

        var targetSnapshots = await AsyncExecuter.ToListAsync(
            targetQuery.Select(t => new
            {
                t.Id,
                t.Type,
                t.CurrentStatus
            }));

        var totalTargets = targetSnapshots.Count;

        var result = new DashboardSummaryDto
        {
            TotalTargets = totalTargets,
            OnlineCount = targetSnapshots.Count(t => t.CurrentStatus == ServiceStatus.Online),
            OfflineCount = targetSnapshots.Count(t => t.CurrentStatus == ServiceStatus.Offline),
            CheckingCount = targetSnapshots.Count(t => t.CurrentStatus == ServiceStatus.Checking),
            RangeStart = rangeStart,
            RangeEnd = rangeEnd
        };

        if (totalTargets == 0)
        {
            result.UptimePercentage = 100;
            result.IncidentsCount = 0;
            return result;
        }

        var targetIds = targetSnapshots.Select(t => t.Id).ToList();
        var outages = await LoadOutagesAsync(targetIds, rangeStart, rangeEnd);

        var rangeSeconds = (rangeEnd - rangeStart).TotalSeconds;
        if (rangeSeconds <= 0)
        {
            result.UptimePercentage = 100;
            result.IncidentsCount = outages.Count;
            return result;
        }

        var downtimeSeconds = outages.Sum(outage => DashboardMetricsHelper.CalculateOverlapSeconds(outage, rangeStart, rangeEnd));
        var denominator = rangeSeconds * totalTargets;
        var uptime = denominator <= 0 ? 1 : Math.Clamp(1 - (downtimeSeconds / denominator), 0, 1);
        result.UptimePercentage = Math.Round(uptime * 100, 2);
        result.IncidentsCount = outages.Count;
        return result;
    }

    public async Task<List<UptimeBucketDto>> GetUptimeSeriesAsync(Guid targetId, DateTime from, DateTime to, string bucket = "day")
    {
        await AuthorizationService.CheckAsync(MonitoringPermissions.Services.View);
        var (rangeStart, rangeEnd) = NormalizeRange(from, to);

        var bucketLength = ResolveBucketLength(bucket);
        var outages = await LoadOutagesAsync(new List<Guid> { targetId }, rangeStart, rangeEnd);

        var buckets = new List<UptimeBucketDto>();
        var cursor = rangeStart;
        while (cursor < rangeEnd)
        {
            var bucketEnd = cursor.Add(bucketLength);
            if (bucketEnd > rangeEnd)
            {
                bucketEnd = rangeEnd;
            }

            var bucketDuration = (bucketEnd - cursor).TotalSeconds;
            double uptimePct = 100;
            if (bucketDuration > 0)
            {
                var downtimeSeconds = outages.Sum(outage => DashboardMetricsHelper.CalculateOverlapSeconds(outage, cursor, bucketEnd));
                var uptime = Math.Clamp(1 - (downtimeSeconds / bucketDuration), 0, 1);
                uptimePct = Math.Round(uptime * 100, 2);
            }

            buckets.Add(new UptimeBucketDto
            {
                Start = cursor,
                End = bucketEnd,
                UptimePercentage = uptimePct
            });

            cursor = bucketEnd;
        }

        return buckets;
    }

    public async Task<List<DashboardIncidentDto>> GetIncidentsAsync(Guid targetId, DateTime from, DateTime to, int skip = 0, int max = 100)
    {
        await AuthorizationService.CheckAsync(MonitoringPermissions.Services.View);
        var (rangeStart, rangeEnd) = NormalizeRange(from, to);
        var sanitizedSkip = skip < 0 ? 0 : skip;
        var sanitizedMax = max <= 0 ? DefaultIncidentPageSize : Math.Min(max, 500);

        var outageQuery = await _outageRepository.GetQueryableAsync();
        outageQuery = outageQuery
            .Where(o => o.TargetId == targetId)
            .Where(o => o.StartedAt <= rangeEnd && (o.EndedAt == null || o.EndedAt >= rangeStart))
            .OrderByDescending(o => o.StartedAt);

        var outages = await AsyncExecuter.ToListAsync(
            outageQuery
                .Skip(sanitizedSkip)
                .Take(sanitizedMax));

        return outages
            .Select(o => new DashboardIncidentDto
            {
                Id = o.Id,
                TargetId = o.TargetId,
                StartedAt = o.StartedAt,
                EndedAt = o.EndedAt,
                FailureCount = o.FailureCount,
                TotalDurationSec = o.TotalDurationSec
            })
            .ToList();
    }

    public async Task<MttrMtbfDto> GetReliabilityAsync(DateTime from, DateTime to, ServiceType? filterType = null)
    {
        await AuthorizationService.CheckAsync(MonitoringPermissions.Services.View);
        var (rangeStart, rangeEnd) = NormalizeRange(from, to);

        var targetQuery = await _targetRepository.GetQueryableAsync();
        if (filterType.HasValue)
        {
            targetQuery = targetQuery.Where(t => t.Type == filterType.Value);
        }

        var targetSnapshots = await AsyncExecuter.ToListAsync(
            targetQuery.Select(t => new
            {
                t.Id,
                t.Type
            }));

        var targetIds = targetSnapshots.Select(t => t.Id).ToList();
        if (!targetIds.Any())
        {
            return new MttrMtbfDto();
        }

        var outages = await LoadOutagesAsync(targetIds, rangeStart, rangeEnd);
        var closedOutages = outages
            .Where(o => o.EndedAt.HasValue)
            .OrderBy(o => o.StartedAt)
            .ToList();

        var result = new MttrMtbfDto();
        if (!closedOutages.Any())
        {
            return result;
        }

        var mttr = DashboardMetricsHelper.CalculateMttrSeconds(closedOutages, rangeStart, rangeEnd);
        if (mttr.HasValue)
        {
            result.MeanTimeToRecoverSeconds = mttr.Value;
        }

        var mtbfDurations = closedOutages
            .GroupBy(o => o.TargetId)
            .SelectMany(group => DashboardMetricsHelper.CalculateMtbfSeconds(group))
            .ToList();

        if (mtbfDurations.Any())
        {
            result.MeanTimeBetweenFailuresSeconds = Math.Round(mtbfDurations.Average(), 2);
        }

        foreach (var serviceGrouping in targetSnapshots.GroupBy(t => t.Type))
        {
            var serviceTargetIds = serviceGrouping.Select(t => t.Id).ToHashSet();
            var serviceOutages = closedOutages.Where(o => serviceTargetIds.Contains(o.TargetId)).ToList();
            if (!serviceOutages.Any())
            {
                continue;
            }

            var serviceMttr = DashboardMetricsHelper.CalculateMttrSeconds(serviceOutages, rangeStart, rangeEnd);
            var serviceMtbfDurations = DashboardMetricsHelper.CalculateMtbfSeconds(serviceOutages);

            result.Breakdown.Add(new MttrMtbfBreakdownDto
            {
                ServiceType = serviceGrouping.Key,
                MeanTimeToRecoverSeconds = serviceMttr,
                MeanTimeBetweenFailuresSeconds = serviceMtbfDurations.Any()
                    ? Math.Round(serviceMtbfDurations.Average(), 2)
                    : null
            });
        }

        return result;
    }

    private (DateTime start, DateTime end) NormalizeRange(DateTime from, DateTime to)
    {
        if (from == default)
        {
            from = Clock.Now.AddDays(-_options.Dashboard.DefaultRangeDays);
        }

        if (to == default)
        {
            to = Clock.Now;
        }

        if (to <= from)
        {
            throw new BusinessException("InvalidRange")
                .WithData("from", from.ToString("O", CultureInfo.InvariantCulture))
                .WithData("to", to.ToString("O", CultureInfo.InvariantCulture));
        }

        var maxRange = TimeSpan.FromDays(Math.Max(1, _options.Dashboard.MaxRangeDays));
        if (to - from > maxRange)
        {
            to = from.Add(maxRange);
        }

        return (from, to);
    }

    private static TimeSpan ResolveBucketLength(string bucket)
    {
        return bucket?.Equals("hour", StringComparison.OrdinalIgnoreCase) == true
            ? TimeSpan.FromHours(1)
            : TimeSpan.FromDays(1);
    }

    private async Task<List<OutageWindow>> LoadOutagesAsync(List<Guid> targetIds, DateTime rangeStart, DateTime rangeEnd)
    {
        if (targetIds.Count == 0)
        {
            return new List<OutageWindow>();
        }

        var outageQuery = await _outageRepository.GetQueryableAsync();
        outageQuery = outageQuery
            .Where(o => targetIds.Contains(o.TargetId))
            .Where(o => o.StartedAt <= rangeEnd && (o.EndedAt == null || o.EndedAt >= rangeStart));

        return await AsyncExecuter.ToListAsync(outageQuery);
    }
}
