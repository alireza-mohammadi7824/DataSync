using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Monitoring.Execution;

public interface IBulkCheckQueue
{
    Guid Enqueue(IEnumerable<Guid> targetIds);

    BulkStatus GetStatus(Guid batchId);

    Task StartAsync(CancellationToken cancellationToken);
}

public sealed record BulkStatus(
    Guid BatchId,
    int Total,
    int Queued,
    int Running,
    int Completed,
    int Failed)
{
    public static BulkStatus Empty(Guid batchId) => new(batchId, 0, 0, 0, 0, 0);
}
