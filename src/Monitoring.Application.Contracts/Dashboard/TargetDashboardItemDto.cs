using System;

namespace Monitoring.Dashboard;

public class TargetDashboardItemDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ServiceType { get; set; } = string.Empty;

    public TargetStatusDto CurrentStatus { get; set; }

    public double Uptime24h { get; set; }

    public double Uptime7d { get; set; }

    public double Uptime30d { get; set; }

    public DateTime? LastOutageStartUtc { get; set; }

    public DateTime? LastOutageEndUtc { get; set; }
}
