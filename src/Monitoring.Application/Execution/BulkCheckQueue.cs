using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monitoring.Options;
using Monitoring.Targets;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace Monitoring.Execution;

public sealed class BulkCheckQueue : IBulkCheckQueue
{
    private readonly Channel<WorkItem> _channel;
    private readonly ConcurrentDictionary<Guid, BatchState> _batches = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ILogger<BulkCheckQueue> _logger;
    private readonly int _maxConcurrentChecks;

    private Task? _processingTask;
    private int _started;

    public BulkCheckQueue(
        IServiceScopeFactory scopeFactory,
        IGuidGenerator guidGenerator,
        IOptions<MonitoringOptions> options,
        ILogger<BulkCheckQueue> logger)
    {
        _scopeFactory = scopeFactory;
        _guidGenerator = guidGenerator;
        _logger = logger;

        _maxConcurrentChecks = Math.Max(1, options.Value.Execution.MaxConcurrentChecks);

        _channel = Channel.CreateUnbounded<WorkItem>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });
    }

    public Guid Enqueue(IEnumerable<Guid> targetIds)
    {
        var ids = targetIds?
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList() ?? new List<Guid>();

        var batchId = _guidGenerator.Create();
        var state = new BatchState(batchId, ids.Count);
        _batches[batchId] = state;

        if (ids.Count == 0)
        {
            state.MarkEmpty();
            return batchId;
        }

        foreach (var id in ids)
        {
            if (!_channel.Writer.TryWrite(new WorkItem(batchId, id)))
            {
                _channel.Writer.TryComplete(new InvalidOperationException("Bulk check queue is unavailable."));
                throw new InvalidOperationException("Unable to enqueue bulk check request.");
            }
        }

        return batchId;
    }

    public BulkStatus GetStatus(Guid batchId)
    {
        return _batches.TryGetValue(batchId, out var state)
            ? state.ToStatus()
            : BulkStatus.Empty(batchId);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
        {
            return _processingTask ?? Task.CompletedTask;
        }

        _processingTask = RunAsync(cancellationToken);
        return _processingTask!;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var semaphore = new SemaphoreSlim(_maxConcurrentChecks, _maxConcurrentChecks);
        var running = new List<Task>();

        try
        {
            await foreach (var work in _channel.Reader.ReadAllAsync(cancellationToken))
            {
                await semaphore.WaitAsync(cancellationToken);

                if (!_batches.TryGetValue(work.BatchId, out var state))
                {
                    semaphore.Release();
                    continue;
                }

                state.Begin();

                var task = ProcessAsync(work, state, semaphore, cancellationToken);
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
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // graceful shutdown
        }

        Task[] remaining;
        lock (running)
        {
            remaining = running.ToArray();
        }

        await Task.WhenAll(remaining);
    }

    private async Task ProcessAsync(
        WorkItem work,
        BatchState state,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IRepository<MonitoringTarget, Guid>>();
            var checkService = scope.ServiceProvider.GetRequiredService<IMonitoringCheckService>();

            var target = await repository.FirstOrDefaultAsync(
                x => x.Id == work.TargetId,
                cancellationToken: cancellationToken);

            if (target == null)
            {
                _logger.LogDebug("Bulk check skipped missing target {TargetId}", work.TargetId);
                state.Complete(success: false);
                return;
            }

            var result = await checkService.RunAsync(target, "bulk", true, cancellationToken);
            var success = !result.IsSkipped && result.Result?.IsSuccess == true;
            state.Complete(success);

            if (result.IsSkipped)
            {
                _logger.LogDebug(
                    "Bulk check skipped target {TargetId}: {Reason}",
                    work.TargetId,
                    result.SkipReason);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (MonitoringCheckConflictException ex)
        {
            _logger.LogDebug(
                ex,
                "Bulk check conflict for target {TargetId}",
                work.TargetId);
            state.Complete(success: false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Bulk check failed for target {TargetId}",
                work.TargetId);
            state.Complete(success: false);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private readonly record struct WorkItem(Guid BatchId, Guid TargetId);

    private sealed class BatchState
    {
        private readonly Guid _batchId;
        private readonly int _total;
        private int _queued;
        private int _running;
        private int _completed;
        private int _failed;

        public BatchState(Guid batchId, int total)
        {
            _batchId = batchId;
            _total = total;
            _queued = total;
        }

        public void MarkEmpty()
        {
            _queued = 0;
            _running = 0;
            _completed = 0;
            _failed = 0;
        }

        public void Begin()
        {
            Interlocked.Decrement(ref _queued);
            Interlocked.Increment(ref _running);
        }

        public void Complete(bool success)
        {
            Interlocked.Decrement(ref _running);
            Interlocked.Increment(ref _completed);

            if (!success)
            {
                Interlocked.Increment(ref _failed);
            }
        }

        public BulkStatus ToStatus()
        {
            return new BulkStatus(
                _batchId,
                _total,
                Math.Max(0, Volatile.Read(ref _queued)),
                Math.Max(0, Volatile.Read(ref _running)),
                Math.Max(0, Volatile.Read(ref _completed)),
                Math.Max(0, Volatile.Read(ref _failed)));
        }
    }
}
