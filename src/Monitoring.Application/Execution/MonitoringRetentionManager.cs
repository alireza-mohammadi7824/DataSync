using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monitoring.Options;
using Monitoring.Targets;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;
using Volo.Abp.Uow;
using Volo.Abp.Threading;

namespace Monitoring.Execution;

public sealed class MonitoringRetentionManager
{
    private readonly IRepository<ServiceStatusHistory, Guid> _historyRepository;
    private readonly IRepository<OutageWindow, Guid> _outageRepository;
    private readonly IClock _clock;
    private readonly MonitoringOptions _options;
    private readonly ExecutionMetrics _metrics;
    private readonly ILogger<MonitoringRetentionManager> _logger;

    public MonitoringRetentionManager(
        IRepository<ServiceStatusHistory, Guid> historyRepository,
        IRepository<OutageWindow, Guid> outageRepository,
        IClock clock,
        IOptions<MonitoringOptions> options,
        ExecutionMetrics metrics,
        ILogger<MonitoringRetentionManager> logger)
    {
        _historyRepository = historyRepository;
        _outageRepository = outageRepository;
        _clock = clock;
        _options = options.Value;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<PurgeSummary> PurgeAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.Now;
        var cutoff = now.AddDays(-_options.Retention.HistoryDays);
        var historyRemoved = 0;
        var outageRemoved = 0;

        if (_options.Retention.HistoryDays > 0)
        {
            historyRemoved += await PurgeHistoryByAgeAsync(cutoff, cancellationToken);
            outageRemoved += await PurgeOutagesByAgeAsync(cutoff, cancellationToken);
        }

        if (_options.Retention.MaxHistoryPerTarget > 0)
        {
            historyRemoved += await PurgeHistoryByLimitAsync(_options.Retention.MaxHistoryPerTarget, cancellationToken);
        }

        var summary = new PurgeSummary(now, historyRemoved, outageRemoved);
        _metrics.AddPurgeSummary(summary);
        _logger.LogInformation(
            "Monitoring retention purge removed {HistoryCount} history rows and {OutageCount} outages",
            historyRemoved,
            outageRemoved);

        return summary;
    }

    private async Task<int> PurgeHistoryByAgeAsync(DateTime cutoff, CancellationToken cancellationToken)
    {
        var batchSize = _options.Retention.PurgeBatchSize;
        var total = 0;
        while (true)
        {
            var queryable = await _historyRepository.GetQueryableAsync();

            var ids = await queryable
                .Where(x => x.ChangedAt < cutoff)
                .OrderBy(x => x.ChangedAt)
                .Select(x => x.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (ids.Count == 0)
            {
                break;
            }

            await _historyRepository.DeleteManyAsync(ids, autoSave: true, cancellationToken: cancellationToken);

            total += ids.Count;

            if (ids.Count < batchSize)
            {
                break;
            }
        }

        return total;
    }

    private async Task<int> PurgeHistoryByLimitAsync(int maxPerTarget, CancellationToken cancellationToken)
    {
        var batchSize = _options.Retention.PurgeBatchSize;
        var total = 0;
        var queryable = await _historyRepository.GetQueryableAsync();

        var overTargets = await queryable
            .GroupBy(x => x.TargetId)
            .Select(g => new { TargetId = g.Key, Count = g.Count() })
            .Where(x => x.Count > maxPerTarget)
            .ToListAsync(cancellationToken);

        foreach (var target in overTargets)
        {
            var removable = Math.Max(0, target.Count - maxPerTarget);
            while (removable > 0)
            {
                var queryable = await _historyRepository.GetQueryableAsync();

                var ids = await queryable
                    .Where(x => x.TargetId == target.TargetId)
                    .OrderByDescending(x => x.ChangedAt)
                    .Skip(maxPerTarget)
                    .Take(Math.Min(removable, batchSize))
                    .Select(x => x.Id)
                    .ToListAsync(cancellationToken);

                if (ids.Count == 0)
                {
                    break;
                }

                await _historyRepository.DeleteManyAsync(ids, autoSave: true, cancellationToken: cancellationToken);

                total += ids.Count;
                removable -= ids.Count;

                if (ids.Count < batchSize)
                {
                    break;
                }
            }
        }

        return total;
    }

    private async Task<int> PurgeOutagesByAgeAsync(DateTime cutoff, CancellationToken cancellationToken)
    {
        var batchSize = _options.Retention.PurgeBatchSize;
        var minOutages = Math.Max(0, _options.Retention.MinOutagesPerTarget);
        var total = 0;
        var queryable = await _outageRepository.GetQueryableAsync();

        var counts = await queryable
            .GroupBy(x => x.TargetId)
            .Select(g => new { TargetId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        foreach (var target in counts)
        {
            var removable = Math.Max(0, target.Count - minOutages);
            if (removable == 0)
            {
                continue;
            }

            while (removable > 0)
            {
                var queryable = await _outageRepository.GetQueryableAsync();

                var ids = await queryable
                    .Where(x => x.TargetId == target.TargetId && x.EndedAt != null && x.EndedAt < cutoff)
                    .OrderBy(x => x.EndedAt)
                    .Take(Math.Min(removable, batchSize))
                    .Select(x => x.Id)
                    .ToListAsync(cancellationToken);

                if (ids.Count == 0)
                {
                    break;
                }

                await _outageRepository.DeleteManyAsync(ids, autoSave: true, cancellationToken: cancellationToken);

                total += ids.Count;
                removable -= ids.Count;

                if (ids.Count < batchSize)
                {
                    break;
                }
            }
        }

        return total;
    }
}
