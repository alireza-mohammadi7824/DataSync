using System;

namespace Monitoring.Targets;

public class CheckBatchStatusDto
{
    public Guid BatchId { get; set; }

    public int TotalTargets { get; set; }

    public int Queued { get; set; }

    public int Running { get; set; }

    public int Completed { get; set; }

    public int Succeeded { get; set; }

    public int Failed { get; set; }

    public int Skipped { get; set; }
}
