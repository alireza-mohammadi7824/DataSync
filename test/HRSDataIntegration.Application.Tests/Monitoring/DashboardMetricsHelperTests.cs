using System;
using Monitoring.Dashboard;
using Xunit;

namespace HRSDataIntegration.Monitoring;

public class DashboardMetricsHelperTests
{
    [Fact]
    public void OverlapUtc_handles_open_outage()
    {
        var now = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var start = now.AddHours(-3);
        var windowStart = now.AddHours(-2);
        var windowEnd = now;

        var overlap = DashboardMetricsHelper.OverlapUtc(start, null, windowStart, windowEnd, now);

        Assert.Equal(TimeSpan.FromHours(2), overlap);
    }

    [Fact]
    public void OverlapUtc_returns_zero_when_outside_window()
    {
        var now = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var start = now.AddHours(-5);
        var end = now.AddHours(-4);
        var windowStart = now.AddHours(-3);
        var windowEnd = now;

        var overlap = DashboardMetricsHelper.OverlapUtc(start, end, windowStart, windowEnd, now);

        Assert.Equal(TimeSpan.Zero, overlap);
    }

    [Fact]
    public void UptimePercent_calculates_remaining_percentage()
    {
        var window = TimeSpan.FromHours(24);
        var downtime = TimeSpan.FromHours(6);

        var uptime = DashboardMetricsHelper.UptimePercent(window, downtime);

        Assert.Equal(75d, uptime);
    }
}
