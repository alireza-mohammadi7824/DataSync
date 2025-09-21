using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Monitoring.Targets;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace Monitoring.Execution;

public sealed class DatabaseTargetRunLock : ITargetRunLock
{
    private readonly IRepository<MonitoringRunLock, Guid> _lockRepository;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly IClock _clock;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ExecutionMetrics _metrics;
    private readonly ILogger<DatabaseTargetRunLock> _logger;
    private readonly string _nodeId;

    public DatabaseTargetRunLock(
        IRepository<MonitoringRunLock, Guid> lockRepository,
        IUnitOfWorkManager unitOfWorkManager,
        IClock clock,
        IGuidGenerator guidGenerator,
        ExecutionMetrics metrics,
        ILogger<DatabaseTargetRunLock> logger)
    {
        _lockRepository = lockRepository;
        _unitOfWorkManager = unitOfWorkManager;
        _clock = clock;
        _guidGenerator = guidGenerator;
        _metrics = metrics;
        _logger = logger;
        _nodeId = $"{Environment.MachineName}-{Guid.NewGuid():N}";
    }

    public async Task<ITargetRunLockHandle?> TryAcquireAsync(Guid targetId, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var now = _clock.Now;
        var expiresAt = now.Add(ttl);

        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);

        var existing = await _lockRepository.FirstOrDefaultAsync(x => x.TargetId == targetId, cancellationToken);
        if (existing != null)
        {
            if (existing.ExpiresAt <= now)
            {
                existing.Refresh(now, expiresAt, _nodeId);
                await _lockRepository.UpdateAsync(existing, autoSave: true, cancellationToken: cancellationToken);
                await uow.CompleteAsync();
                _logger.LogDebug("Refreshed run lock for target {TargetId} on node {NodeId}", targetId, _nodeId);
                return new TargetRunLockHandle(this, existing.Id, targetId, cancellationToken);
            }

            _metrics.IncrementLocksContended();
            _logger.LogInformation("Run lock contention for target {TargetId} on node {NodeId}", targetId, _nodeId);
            return null;
        }

        var lockId = _guidGenerator.Create();
        var entity = new MonitoringRunLock(lockId, targetId, now, expiresAt, _nodeId);

        try
        {
            await _lockRepository.InsertAsync(entity, autoSave: true, cancellationToken: cancellationToken);
            await uow.CompleteAsync();
            _logger.LogDebug("Acquired run lock for target {TargetId} on node {NodeId}", targetId, _nodeId);
            return new TargetRunLockHandle(this, lockId, targetId, cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _metrics.IncrementLocksContended();
            _logger.LogInformation(
                "Run lock contention for target {TargetId} on node {NodeId}",
                targetId,
                _nodeId);
            _logger.LogDebug(ex,
                "Run lock contention exception details for target {TargetId}",
                targetId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Unexpected error while acquiring run lock for target {TargetId} on node {NodeId}",
                targetId,
                _nodeId);
            throw;
        }
    }

    private async ValueTask ReleaseAsync(Guid lockId, Guid targetId, CancellationToken cancellationToken)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        var entity = await _lockRepository.FirstOrDefaultAsync(x => x.Id == lockId, cancellationToken);
        if (entity != null && entity.NodeId == _nodeId && entity.TargetId == targetId)
        {
            await _lockRepository.DeleteAsync(entity, autoSave: true, cancellationToken: cancellationToken);
            await uow.CompleteAsync();
            _logger.LogDebug("Released run lock for target {TargetId} on node {NodeId}", targetId, _nodeId);
            return;
        }

        await uow.CompleteAsync();
    }

    private sealed class TargetRunLockHandle : ITargetRunLockHandle
    {
        private readonly DatabaseTargetRunLock _owner;
        private readonly Guid _lockId;
        private readonly CancellationToken _cancellationToken;
        private bool _disposed;

        public TargetRunLockHandle(DatabaseTargetRunLock owner, Guid lockId, Guid targetId, CancellationToken cancellationToken)
        {
            _owner = owner;
            _lockId = lockId;
            TargetId = targetId;
            _cancellationToken = cancellationToken;
        }

        public Guid TargetId { get; }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            await _owner.ReleaseAsync(_lockId, TargetId, _cancellationToken);
        }
    }
}
