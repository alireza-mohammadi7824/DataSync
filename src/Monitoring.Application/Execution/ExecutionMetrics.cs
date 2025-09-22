using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Monitoring.Execution;

public sealed class ExecutionMetrics
{
    private long _checksStarted;
    private long _checksSucceeded;
    private long _checksFailed;
    private long _checksSkipped;
    private long _locksContended;

    private readonly ConcurrentQueue<PurgeSummary> _purgeSummaries = new();

    public void IncrementChecksStarted() => Interlocked.Increment(ref _checksStarted);

    public void IncrementChecksSucceeded() => Interlocked.Increment(ref _checksSucceeded);

    public void IncrementChecksFailed() => Interlocked.Increment(ref _checksFailed);

    public void IncrementChecksSkipped() => Interlocked.Increment(ref _checksSkipped);

    public void IncrementLocksContended() => Interlocked.Increment(ref _locksContended);

    public void AddPurgeSummary(PurgeSummary summary)
    {
        _purgeSummaries.Enqueue(summary);

        while (_purgeSummaries.Count > 5)
        {
            _purgeSummaries.TryDequeue(out _);
        }
    }

    public ExecutionMetricsSnapshot CreateSnapshot()
    {
        var purges = _purgeSummaries.ToArray();
        return new ExecutionMetricsSnapshot(
            Interlocked.Read(ref _checksStarted),
            Interlocked.Read(ref _checksSucceeded),
            Interlocked.Read(ref _checksFailed),
            Interlocked.Read(ref _checksSkipped),
            Interlocked.Read(ref _locksContended),
            purges);
    }
}

public readonly record struct ExecutionMetricsSnapshot(
    long ChecksStarted,
    long ChecksSucceeded,
    long ChecksFailed,
    long ChecksSkipped,
    long LocksContended,
    IReadOnlyList<PurgeSummary> RecentPurges);

public readonly record struct PurgeSummary(DateTime CompletedAt, int HistoryRemoved, int OutagesRemoved);
