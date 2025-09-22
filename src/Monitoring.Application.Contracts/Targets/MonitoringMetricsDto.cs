using System.Collections.Generic;

namespace Monitoring.Targets;

public class MonitoringMetricsDto
{
    public long ChecksStarted { get; set; }

    public long ChecksSucceeded { get; set; }

    public long ChecksFailed { get; set; }

    public long ChecksSkipped { get; set; }

    public long LocksContended { get; set; }

    public List<PurgeSummaryDto> PurgeSummaries { get; set; } = new();
}
