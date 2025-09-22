using System;

namespace Monitoring.Dashboard;

public static class DashboardMetricsHelper
{
    public static TimeSpan OverlapUtc(DateTime start, DateTime? end, DateTime winStart, DateTime winEnd, DateTime nowUtc)
    {
        var effectiveEnd = end ?? nowUtc;
        if (effectiveEnd <= winStart || start >= winEnd)
        {
            return TimeSpan.Zero;
        }

        var clippedStart = start < winStart ? winStart : start;
        var clippedEnd = effectiveEnd > winEnd ? winEnd : effectiveEnd;
        var duration = clippedEnd - clippedStart;
        return duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
    }

    public static double UptimePercent(TimeSpan window, TimeSpan downtime)
    {
        if (window <= TimeSpan.Zero)
        {
            return 100d;
        }

        var remaining = window - downtime;
        if (remaining <= TimeSpan.Zero)
        {
            return 0d;
        }

        var uptimeRatio = remaining.TotalSeconds / window.TotalSeconds;
        return Math.Max(0d, Math.Min(100d, uptimeRatio * 100d));
    }
}
