using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Monitoring.Permissions;
using Monitoring.Targets;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Validation;

namespace Monitoring.History;

[Authorize(MonitoringPermissions.History.View)]
public class HistoryAppService : ApplicationService, IHistoryAppService
{
    private readonly IRepository<MonitoringTarget, Guid> _targetRepository;
    private readonly IRepository<ServiceStatusHistory, Guid> _historyRepository;
    private readonly IRepository<OutageWindow, Guid> _outageRepository;

    public HistoryAppService(
        IRepository<MonitoringTarget, Guid> targetRepository,
        IRepository<ServiceStatusHistory, Guid> historyRepository,
        IRepository<OutageWindow, Guid> outageRepository)
    {
        _targetRepository = targetRepository;
        _historyRepository = historyRepository;
        _outageRepository = outageRepository;
    }

    public async Task<OutageListDto> GetOutagesAsync(Guid id, int? count, CancellationToken cancellationToken)
    {
        await EnsureTargetExistsAsync(id, cancellationToken);

        var normalizedCount = count.HasValue ? Math.Clamp(count.Value, 1, 100) : 10;

        var outageQueryable = await _outageRepository.GetQueryableAsync();

        var outages = await outageQueryable
            .Where(o => o.TargetId == id)
            .OrderByDescending(o => o.StartedAt)
            .Take(normalizedCount)
            .ToListAsync(cancellationToken);

        if (outages.Count == 0)
        {
            return new OutageListDto(Array.Empty<OutageDto>());
        }

        var nowUtc = Clock.Normalize(Clock.Now);
        var maxEnd = outages.Max(o => o.EndedAt ?? nowUtc);
        var minStart = outages.Min(o => o.StartedAt);

        var historyQueryable = await _historyRepository.GetQueryableAsync();
        var historyEntries = await historyQueryable
            .Where(h => h.TargetId == id)
            .Where(h => h.ChangedAt >= minStart && h.ChangedAt <= maxEnd)
            .OrderBy(h => h.ChangedAt)
            .ToListAsync(cancellationToken);

        var historyWithReason = historyEntries
            .Where(h => !string.IsNullOrWhiteSpace(h.ErrorSummary))
            .ToList();

        var items = new List<OutageDto>(outages.Count);

        foreach (var outage in outages)
        {
            var outageEnd = outage.EndedAt ?? nowUtc;
            if (outageEnd < outage.StartedAt)
            {
                outageEnd = outage.StartedAt;
            }

            var duration = outageEnd - outage.StartedAt;
            if (duration < TimeSpan.Zero)
            {
                duration = TimeSpan.Zero;
            }

            string? reason = null;
            if (historyWithReason.Count > 0)
            {
                reason = historyWithReason
                    .Where(h => h.ChangedAt >= outage.StartedAt && h.ChangedAt <= outageEnd)
                    .Select(h => h.ErrorSummary)
                    .FirstOrDefault();
            }

            items.Add(new OutageDto
            {
                Id = outage.Id,
                StartedAtUtc = outage.StartedAt,
                EndedAtUtc = outage.EndedAt,
                Duration = duration,
                Reason = reason
            });
        }

        return new OutageListDto(items);
    }

    public async Task<TimelineDto> GetTimelineAsync(Guid id, TimelineRequestDto input, CancellationToken cancellationToken)
    {
        if (input == null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        if (input.ToUtc <= input.FromUtc)
        {
            throw new AbpValidationException("Timeline range is invalid.");
        }

        var target = await EnsureTargetExistsAsync(id, cancellationToken);

        var nowUtc = Clock.Normalize(Clock.Now);
        var fromUtc = input.FromUtc;
        var toUtc = input.ToUtc > nowUtc ? nowUtc : input.ToUtc;

        if (toUtc <= fromUtc)
        {
            return new TimelineDto(Array.Empty<TimelineIntervalDto>());
        }

        var historyQueryable = await _historyRepository.GetQueryableAsync();
        historyQueryable = historyQueryable.Where(h => h.TargetId == id);

        var previousChange = await historyQueryable
            .Where(h => h.ChangedAt < fromUtc)
            .OrderByDescending(h => h.ChangedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var rangeChanges = await historyQueryable
            .Where(h => h.ChangedAt >= fromUtc && h.ChangedAt <= toUtc)
            .OrderBy(h => h.ChangedAt)
            .ToListAsync(cancellationToken);

        var initialStatus = previousChange?.ToStatus ?? target.CurrentStatus;

        var intervals = BuildTimeline(initialStatus, rangeChanges, fromUtc, toUtc);

        return new TimelineDto(intervals);
    }

    private async Task<MonitoringTarget> EnsureTargetExistsAsync(Guid id, CancellationToken cancellationToken)
    {
        var targetQueryable = await _targetRepository.GetQueryableAsync();
        var target = await targetQueryable
            .Where(t => t.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        if (target == null)
        {
            throw new EntityNotFoundException(typeof(MonitoringTarget), id);
        }

        return target;
    }

    private static IReadOnlyList<TimelineIntervalDto> BuildTimeline(
        ServiceStatus initialStatus,
        List<ServiceStatusHistory> changes,
        DateTime fromUtc,
        DateTime toUtc)
    {
        if (toUtc <= fromUtc)
        {
            return Array.Empty<TimelineIntervalDto>();
        }

        var intervals = new List<TimelineIntervalDto>();
        var currentStatus = initialStatus;
        var cursor = fromUtc;

        foreach (var change in changes)
        {
            var changeTime = change.ChangedAt;
            if (changeTime <= cursor)
            {
                currentStatus = change.ToStatus;
                continue;
            }

            var intervalEnd = changeTime < toUtc ? changeTime : toUtc;
            if (intervalEnd > cursor)
            {
                intervals.Add(new TimelineIntervalDto
                {
                    StartUtc = cursor,
                    EndUtc = intervalEnd,
                    Status = MapStatus(currentStatus)
                });
            }

            currentStatus = change.ToStatus;
            cursor = changeTime;

            if (cursor >= toUtc)
            {
                break;
            }
        }

        if (cursor < toUtc)
        {
            intervals.Add(new TimelineIntervalDto
            {
                StartUtc = cursor,
                EndUtc = toUtc,
                Status = MapStatus(currentStatus)
            });
        }

        return MergeIntervals(intervals);
    }

    private static IReadOnlyList<TimelineIntervalDto> MergeIntervals(List<TimelineIntervalDto> intervals)
    {
        if (intervals.Count == 0)
        {
            return Array.Empty<TimelineIntervalDto>();
        }

        var ordered = intervals
            .Where(interval => interval.EndUtc > interval.StartUtc)
            .OrderBy(interval => interval.StartUtc)
            .ToList();

        if (ordered.Count == 0)
        {
            return Array.Empty<TimelineIntervalDto>();
        }

        var merged = new List<TimelineIntervalDto> { ordered[0] };

        for (var i = 1; i < ordered.Count; i++)
        {
            var current = ordered[i];
            var last = merged[merged.Count - 1];

            if (last.Status == current.Status && last.EndUtc >= current.StartUtc)
            {
                merged[merged.Count - 1] = new TimelineIntervalDto
                {
                    StartUtc = last.StartUtc,
                    EndUtc = last.EndUtc > current.EndUtc ? last.EndUtc : current.EndUtc,
                    Status = last.Status
                };
            }
            else if (last.Status == current.Status && last.EndUtc == current.StartUtc)
            {
                merged[merged.Count - 1] = new TimelineIntervalDto
                {
                    StartUtc = last.StartUtc,
                    EndUtc = current.EndUtc,
                    Status = last.Status
                };
            }
            else
            {
                merged.Add(current);
            }
        }

        return merged;
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
}
