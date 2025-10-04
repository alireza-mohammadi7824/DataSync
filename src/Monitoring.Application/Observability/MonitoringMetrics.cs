namespace Monitoring.Observability;

public sealed class MonitoringMetrics
{
    private long _checksStarted;
    private long _checksSucceeded;
    private long _checksFailed;
    private long _checksSkipped;
    private long _locksContended;

    public void IncChecksStarted() => System.Threading.Interlocked.Increment(ref _checksStarted);

    public void IncChecksSucceeded() => System.Threading.Interlocked.Increment(ref _checksSucceeded);

    public void IncChecksFailed() => System.Threading.Interlocked.Increment(ref _checksFailed);

    public void IncChecksSkipped() => System.Threading.Interlocked.Increment(ref _checksSkipped);

    public void IncLocksContended() => System.Threading.Interlocked.Increment(ref _locksContended);

    public (long started, long succeeded, long failed, long skipped, long locksContended) Snapshot()
        => (System.Threading.Interlocked.Read(ref _checksStarted),
            System.Threading.Interlocked.Read(ref _checksSucceeded),
            System.Threading.Interlocked.Read(ref _checksFailed),
            System.Threading.Interlocked.Read(ref _checksSkipped),
            System.Threading.Interlocked.Read(ref _locksContended));
}
