using System;
using Volo.Abp.Domain.Entities;

namespace Monitoring.Targets;

public class MonitoringRunLock : Entity<Guid>
{
    public Guid TargetId { get; protected set; }

    public DateTime LockedAt { get; protected set; }

    public DateTime ExpiresAt { get; protected set; }

    public string NodeId { get; protected set; }

    protected MonitoringRunLock()
    {
    }

    public MonitoringRunLock(Guid id, Guid targetId, DateTime lockedAt, DateTime expiresAt, string nodeId)
        : base(id)
    {
        TargetId = targetId;
        LockedAt = lockedAt;
        ExpiresAt = expiresAt;
        NodeId = nodeId;
    }

    public void Refresh(DateTime lockedAt, DateTime expiresAt, string nodeId)
    {
        LockedAt = lockedAt;
        ExpiresAt = expiresAt;
        NodeId = nodeId;
    }
}
