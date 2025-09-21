using System;

namespace Monitoring.Targets;

public class PurgeSummaryDto
{
    public DateTime CompletedAt { get; set; }

    public int HistoryRemoved { get; set; }

    public int OutagesRemoved { get; set; }
}
