using System;
using System.Threading;
using System.Threading.Tasks;

namespace Monitoring.Execution;

public interface ITargetRunLock
{
    Task<ITargetRunLockHandle?> TryAcquireAsync(Guid targetId, TimeSpan ttl, CancellationToken cancellationToken = default);
}

public interface ITargetRunLockHandle : IAsyncDisposable
{
    Guid TargetId { get; }
}
