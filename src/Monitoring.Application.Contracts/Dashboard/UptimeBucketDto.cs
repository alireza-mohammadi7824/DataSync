using System;

namespace Monitoring.Dashboard;

public class UptimeBucketDto
{
    public DateTime Start { get; set; }

    public DateTime End { get; set; }

    public double UptimePercentage { get; set; }
}
