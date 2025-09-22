using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monitoring.Options;
using Monitoring.Targets;
using Volo.Abp.Domain.Repositories;

namespace Monitoring.Execution;

public sealed class BulkCheckProcessor : BackgroundService
{
    private readonly BulkCheckQueue _queue;
    private readonly IRepository<MonitoringTarget, Guid> _targetRepository;
    private readonly IMonitoringCheckService _checkService;
    private readonly MonitoringOptions _options;
    private readonly ILogger<BulkCheckProcessor> _logger;

    public BulkCheckProcessor(
        BulkCheckQueue queue,
        IRepository<MonitoringTarget, Guid> targetRepository,
        IMonitoringCheckService checkService,
        IOptions<MonitoringOptions> options,
        ILogger<BulkCheckProcessor> logger)
    {
        _queue = queue;
        _targetRepository = targetRepository;
        _checkService = checkService;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var concurrency = Math.Max(1, _options.Execution.MaxConcurrentChecks);
        using var semaphore = new SemaphoreSlim(concurrency, concurrency);
        var running = new List<Task>();

        await foreach (var workItem in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            await semaphore.WaitAsync(stoppingToken);

            var task = ProcessWorkItemAsync(workItem, semaphore, stoppingToken);
            lock (running)
            {
                running.Add(task);
            }

            _ = task.ContinueWith(
                t =>
                {
                    lock (running)
                    {
                        running.Remove(t);
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        Task[] remaining;
        lock (running)
        {
            remaining = running.ToArray();
        }

        await Task.WhenAll(remaining);
    }

    private async Task ProcessWorkItemAsync(BulkCheckQueue.CheckWorkItem workItem, SemaphoreSlim semaphore, CancellationToken stoppingToken)
    {
        try
        {
            if (!_queue.TryBegin(workItem.BatchId))
            {
                return;
            }

            var target = await _targetRepository.FirstOrDefaultAsync(x => x.Id == workItem.TargetId, cancellationToken: stoppingToken);
            if (target == null)
            {
                _logger.LogInformation(
                    "Bulk check skipped target {TargetId} because it no longer exists",
                    workItem.TargetId);
                _queue.Complete(workItem.BatchId, success: false, skipped: true);
                return;
            }

            var result = await _checkService.RunAsync(target, "bulk", true, stoppingToken);
            if (result.IsSkipped)
            {
                _logger.LogDebug(
                    "Bulk check skipped target {TargetId}: {Reason}",
                    workItem.TargetId,
                    result.SkipReason);
            }
            _queue.Complete(workItem.BatchId, result.IsSuccess, result.IsSkipped);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (MonitoringCheckConflictException ex)
        {
            _queue.Complete(workItem.BatchId, success: false, skipped: true);
            _logger.LogDebug(
                ex,
                "Bulk check skipped target {TargetId} due to lock contention",
                workItem.TargetId);
        }
        catch (Exception ex)
        {
            _queue.Complete(workItem.BatchId, success: false, skipped: false);
            _logger.LogWarning(ex, "Bulk check failed for target {TargetId}", workItem.TargetId);
        }
        finally
        {
            semaphore.Release();
        }
    }
}
