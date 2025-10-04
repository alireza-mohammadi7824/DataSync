using System;

namespace Monitoring.Dashboard;

public class DashboardSummaryDto
{
    public double Uptime24h { get; set; }

    public double Uptime7d { get; set; }

    public double Uptime30d { get; set; }

    public double Mttr30d { get; set; }

    public double Mtbf30d { get; set; }

    public int OnlineCount { get; set; }

    public int OfflineCount { get; set; }

    public int CheckingCount { get; set; }

    public DateTime GeneratedAtUtc { get; set; }
}
