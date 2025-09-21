using System;

namespace Monitoring.Targets;

public class CheckBatchEnqueueResultDto
{
    public Guid BatchId { get; set; }

    public int TotalTargets { get; set; }
}
