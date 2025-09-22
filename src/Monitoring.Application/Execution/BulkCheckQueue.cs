using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Monitoring.Options;
using Volo.Abp.Guids;

namespace Monitoring.Execution;

public sealed class BulkCheckQueue : IBulkCheckQueue
{
    private readonly Channel<CheckWorkItem> _channel;
    private readonly ConcurrentDictionary<Guid, BatchState> _batches = new();
    private readonly IGuidGenerator _guidGenerator;

    public BulkCheckQueue(IGuidGenerator guidGenerator, IOptions<MonitoringOptions> options)
    {
        _guidGenerator = guidGenerator;
        var execution = options.Value.Execution;
        var maxConcurrent = Math.Max(1, execution.MaxConcurrentChecks);
        var capacity = Math.Max(maxConcurrent * 4, maxConcurrent);
        _channel = Channel.CreateBounded<CheckWorkItem>(new BoundedChannelOptions(capacity)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    internal ChannelReader<CheckWorkItem> Reader => _channel.Reader;

    public async Task<CheckBatchEnqueueResult> EnqueueAsync(IEnumerable<Guid> targetIds, CancellationToken cancellationToken = default)
    {
        var ids = targetIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        var batchId = _guidGenerator.Create();
        var state = new BatchState(batchId, ids.Count);
        _batches[batchId] = state;

        if (ids.Count == 0)
        {
            state.MarkEmpty();
            return new CheckBatchEnqueueResult(batchId, 0);
        }

        foreach (var id in ids)
        {
            await _channel.Writer.WriteAsync(new CheckWorkItem(batchId, id), cancellationToken);
        }

        return new CheckBatchEnqueueResult(batchId, ids.Count);
    }

    public CheckBatchStatus GetStatus(Guid batchId)
    {
        return _batches.TryGetValue(batchId, out var state)
            ? state.ToStatus()
            : CheckBatchStatus.Empty(batchId);
    }

    internal bool TryBegin(Guid batchId)
    {
        if (_batches.TryGetValue(batchId, out var state))
        {
            state.BeginTarget();
            return true;
        }

        return false;
    }

    internal void Complete(Guid batchId, bool success, bool skipped)
    {
        if (_batches.TryGetValue(batchId, out var state))
        {
            state.CompleteTarget(success, skipped);
        }
    }

    internal readonly record struct CheckWorkItem(Guid BatchId, Guid TargetId);

    private sealed class BatchState
    {
        private readonly Guid _batchId;
        private long _queued;
        private long _running;
        private long _completed;
        private long _succeeded;
        private long _failed;
        private long _skipped;

        public BatchState(Guid batchId, int total)
        {
            _batchId = batchId;
            Total = total;
            _queued = total;
        }

        public int Total { get; private set; }

        public void MarkEmpty()
        {
            Total = 0;
            _queued = 0;
            _running = 0;
            _completed = 0;
        }

        public void BeginTarget()
        {
            Interlocked.Decrement(ref _queued);
            Interlocked.Increment(ref _running);
        }

        public void CompleteTarget(bool success, bool skipped)
        {
            Interlocked.Decrement(ref _running);
            Interlocked.Increment(ref _completed);

            if (skipped)
            {
                Interlocked.Increment(ref _skipped);
            }
            else if (success)
            {
                Interlocked.Increment(ref _succeeded);
            }
            else
            {
                Interlocked.Increment(ref _failed);
            }
        }

        public CheckBatchStatus ToStatus()
        {
            return new CheckBatchStatus(
                _batchId,
                Total,
                (int)Interlocked.Read(ref _queued),
                (int)Interlocked.Read(ref _running),
                (int)Interlocked.Read(ref _completed),
                (int)Interlocked.Read(ref _succeeded),
                (int)Interlocked.Read(ref _failed),
                (int)Interlocked.Read(ref _skipped));
        }
    }
}
