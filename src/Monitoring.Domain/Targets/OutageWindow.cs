using System;
using Volo.Abp.Domain.Entities;

namespace Monitoring.Targets;

public class OutageWindow : Entity<Guid>
{
    public Guid TargetId { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? EndedAt { get; private set; }
    public int FailureCount { get; private set; }
    public int? TotalDurationSec { get; private set; }
    public DateTime? LastAlertAt { get; private set; }
    public int AlertsSent { get; private set; }

    public OutageWindow()
    {
    }

    public OutageWindow(Guid id, Guid targetId, DateTime startedAt, int failureCount)
        : base(id)
    {
        TargetId = targetId;
        MarkAsStarted(startedAt, failureCount);
    }

    public void IncrementFailure()
    {
        FailureCount++;
    }

    public void Close(DateTime endedAt)
    {
        if (EndedAt.HasValue)
        {
            return;
        }

        EndedAt = endedAt;
        var duration = (int)Math.Max(0, (EndedAt.Value - StartedAt).TotalSeconds);
        TotalDurationSec = duration;
    }

    public void MarkAsStarted(DateTime startedAt, int initialFailureCount)
    {
        StartedAt = startedAt;
        EndedAt = null;
        FailureCount = initialFailureCount < 1 ? 1 : initialFailureCount;
        TotalDurationSec = null;
        ResetAlerts();
    }

    public void RecordAlert(DateTime timestamp)
    {
        LastAlertAt = timestamp;
        AlertsSent++;
    }

    public void ResetAlerts()
    {
        LastAlertAt = null;
        AlertsSent = 0;
    }
}
