using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Monitoring.Execution;

public interface IBulkCheckQueue
{
    Task<CheckBatchEnqueueResult> EnqueueAsync(IEnumerable<Guid> targetIds, CancellationToken cancellationToken = default);

    CheckBatchStatus GetStatus(Guid batchId);
}

public readonly record struct CheckBatchEnqueueResult(Guid BatchId, int TotalQueued);

public readonly record struct CheckBatchStatus(
    Guid BatchId,
    int Total,
    int Queued,
    int Running,
    int Completed,
    int Succeeded,
    int Failed,
    int Skipped)
{
    public static CheckBatchStatus Empty(Guid batchId) => new(batchId, 0, 0, 0, 0, 0, 0, 0);
}
