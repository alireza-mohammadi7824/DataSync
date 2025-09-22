using System;

namespace Monitoring.Dashboard;

public static class DashboardMetricsHelper
{
    public static TimeSpan OverlapUtc(DateTime start, DateTime? end, DateTime winStart, DateTime winEnd, DateTime nowUtc)
    {
        var e = end ?? nowUtc;
        if (e <= winStart || start >= winEnd)
        {
            return TimeSpan.Zero;
        }

        var a = start < winStart ? winStart : start;
        var b = e > winEnd ? winEnd : e;
        var dur = b - a;
        return dur < TimeSpan.Zero ? TimeSpan.Zero : dur;
    }

    public static double UptimePercent(TimeSpan window, TimeSpan downtime)
        => window <= TimeSpan.Zero ? 100d : Math.Max(0d, 100d * (window - downtime).TotalSeconds / window.TotalSeconds);
}
