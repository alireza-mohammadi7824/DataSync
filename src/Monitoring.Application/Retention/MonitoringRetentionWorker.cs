using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monitoring.Options;
using Monitoring.Targets;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Timing;

namespace Monitoring.Retention;

public sealed class MonitoringRetentionWorker : BackgroundService
{
    private static readonly (int Hour, int Minute) DefaultSchedule = (2, 30);

    private readonly ILogger<MonitoringRetentionWorker> _logger;
    private readonly IClock _clock;
    private readonly IRepository<ServiceStatusHistory, Guid> _historyRepository;
    private readonly IRepository<OutageWindow, Guid> _outageRepository;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IOptionsMonitor<MonitoringRetentionOptions> _options;

    public MonitoringRetentionWorker(
        ILogger<MonitoringRetentionWorker> logger,
        IClock clock,
        IRepository<ServiceStatusHistory, Guid> historyRepository,
        IRepository<OutageWindow, Guid> outageRepository,
        IAsyncQueryableExecuter asyncExecuter,
        IOptionsMonitor<MonitoringRetentionOptions> options)
    {
        _logger = logger;
        _clock = clock;
        _historyRepository = historyRepository;
        _outageRepository = outageRepository;
        _asyncExecuter = asyncExecuter;
        _options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var nowUtc = _clock.Now.ToUniversalTime();
            var schedule = ResolveSchedule(_options.CurrentValue.ScheduleUtc);
            var next = NextUtcTime(nowUtc, schedule.Hour, schedule.Minute);
            var delay = next - nowUtc;

            if (delay > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                var optionsSnapshot = _options.CurrentValue;
                var sw = Stopwatch.StartNew();
                var (purgedHistory, trimmedHistory, purgedOutages) = await RunOnceAsync(optionsSnapshot, stoppingToken);
                sw.Stop();

                _logger.LogInformation(
                    "Retention purge completed: purgedHistory={PurgedHist}, trimmedHistory={TrimmedHist}, purgedOutages={PurgedOutages}, durationMs={Ms}",
                    purgedHistory,
                    trimmedHistory,
                    purgedOutages,
                    sw.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Retention purge run failed");
            }
        }
    }

    private static DateTime NextUtcTime(DateTime nowUtc, int hour, int minute)
    {
        var next = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, hour, minute, 0, DateTimeKind.Utc);
        if (next <= nowUtc)
        {
            next = next.AddDays(1);
        }

        return next;
    }

    private static (int Hour, int Minute) ResolveSchedule(string? schedule)
    {
        if (!string.IsNullOrWhiteSpace(schedule) &&
            TimeSpan.TryParseExact(schedule, "hh\\:mm", CultureInfo.InvariantCulture, out var parsed))
        {
            var hour = Math.Clamp(parsed.Hours, 0, 23);
            var minute = Math.Clamp(parsed.Minutes, 0, 59);
            return (hour, minute);
        }

        return DefaultSchedule;
    }

    private async Task<(int purgedHist, int trimmedHist, int purgedOutages)> RunOnceAsync(
        MonitoringRetentionOptions options,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var nowUtc = _clock.Now.ToUniversalTime();
        var cutoffUtc = nowUtc - TimeSpan.FromDays(options.HistoryDays);

        var purgedHistory = await PurgeHistoryByAgeAsync(cutoffUtc, options.PurgeBatchSize, ct);
        var trimmedHistory = await TrimHistoryByCapAsync(options.MaxHistoryPerTarget, options.PurgeBatchSize, ct);
        var purgedOutages = await PurgeOutagesAsync(cutoffUtc, options.KeepLastOutagesPerTarget, options.PurgeBatchSize, ct);

        return (purgedHistory, trimmedHistory, purgedOutages);
    }

    private async Task<int> PurgeHistoryByAgeAsync(DateTime cutoffUtc, int batchSize, CancellationToken ct)
    {
        var total = 0;
        var queryable = await _historyRepository.GetQueryableAsync();

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var ids = await _asyncExecuter.ToListAsync(
                queryable
                    .Where(history => history.ChangedAt < cutoffUtc)
                    .OrderBy(history => history.ChangedAt)
                    .Select(history => history.Id)
                    .Take(batchSize),
                ct);

            if (ids.Count == 0)
            {
                break;
            }

            await _historyRepository.DeleteManyAsync(ids, autoSave: true, cancellationToken: ct);
            total += ids.Count;

            if (ids.Count < batchSize)
            {
                break;
            }
        }

        return total;
    }

    private async Task<int> TrimHistoryByCapAsync(int maxPerTarget, int batchSize, CancellationToken ct)
    {
        if (maxPerTarget <= 0)
        {
            return 0;
        }

        var total = 0;
        var queryable = await _historyRepository.GetQueryableAsync();

        var overTargets = await _asyncExecuter.ToListAsync(
            queryable
                .GroupBy(history => history.TargetId)
                .Select(group => new { TargetId = group.Key, Count = group.Count() })
                .Where(result => result.Count > maxPerTarget),
            ct);

        foreach (var target in overTargets)
        {
            ct.ThrowIfCancellationRequested();

            var removable = target.Count - maxPerTarget;
            while (removable > 0)
            {
                ct.ThrowIfCancellationRequested();

                var take = Math.Min(removable, batchSize);
                var ids = await _asyncExecuter.ToListAsync(
                    queryable
                        .Where(history => history.TargetId == target.TargetId)
                        .OrderBy(history => history.ChangedAt)
                        .Select(history => history.Id)
                        .Take(take),
                    ct);

                if (ids.Count == 0)
                {
                    break;
                }

                await _historyRepository.DeleteManyAsync(ids, autoSave: true, cancellationToken: ct);

                total += ids.Count;
                removable -= ids.Count;

                if (ids.Count < take)
                {
                    break;
                }
            }
        }

        return total;
    }

    private async Task<int> PurgeOutagesAsync(DateTime cutoffUtc, int keepPerTarget, int batchSize, CancellationToken ct)
    {
        var total = 0;
        var keep = Math.Max(0, keepPerTarget);
        var queryable = await _outageRepository.GetQueryableAsync();

        var targetIds = await _asyncExecuter.ToListAsync(
            queryable
                .Where(outage => outage.EndedAt != null && outage.EndedAt < cutoffUtc)
                .Select(outage => outage.TargetId)
                .Distinct(),
            ct);

        foreach (var targetId in targetIds)
        {
            ct.ThrowIfCancellationRequested();

            List<Guid> keepIds = keep > 0
                ? await _asyncExecuter.ToListAsync(
                    queryable
                        .Where(outage => outage.TargetId == targetId)
                        .OrderByDescending(outage => outage.StartedAt)
                        .Take(keep)
                        .Select(outage => outage.Id),
                    ct)
                : new List<Guid>();

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                var candidateQuery = queryable
                    .Where(outage => outage.TargetId == targetId && outage.EndedAt != null && outage.EndedAt < cutoffUtc);

                if (keepIds.Count > 0)
                {
                    candidateQuery = candidateQuery.Where(outage => !keepIds.Contains(outage.Id));
                }

                var ids = await _asyncExecuter.ToListAsync(
                    candidateQuery
                        .OrderBy(outage => outage.StartedAt)
                        .Select(outage => outage.Id)
                        .Take(batchSize),
                    ct);

                if (ids.Count == 0)
                {
                    break;
                }

                await _outageRepository.DeleteManyAsync(ids, autoSave: true, cancellationToken: ct);

                total += ids.Count;

                if (ids.Count < batchSize)
                {
                    break;
                }
            }
        }

        return total;
    }
}
