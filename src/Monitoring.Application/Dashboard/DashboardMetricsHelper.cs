using System;
using System.Collections.Generic;
using System.Linq;
using Monitoring.Targets;

namespace Monitoring.Dashboard;

internal static class DashboardMetricsHelper
{
    public static double CalculateOverlapSeconds(OutageWindow outage, DateTime start, DateTime end)
    {
        var overlapStart = outage.StartedAt > start ? outage.StartedAt : start;
        var outageEnd = outage.EndedAt ?? end;
        var overlapEnd = outageEnd < end ? outageEnd : end;

        if (overlapEnd <= overlapStart)
        {
            return 0;
        }

        return (overlapEnd - overlapStart).TotalSeconds;
    }

    public static double? CalculateMttrSeconds(IEnumerable<OutageWindow> outages, DateTime rangeStart, DateTime rangeEnd)
    {
        var durations = outages
            .Where(outage => outage.EndedAt.HasValue || outage.TotalDurationSec.HasValue)
            .Select(outage => outage.TotalDurationSec ?? (int)Math.Max(0, (Math.Min(rangeEnd.Ticks, (outage.EndedAt ?? rangeEnd).Ticks) - Math.Max(rangeStart.Ticks, outage.StartedAt.Ticks)) / TimeSpan.TicksPerSecond))
            .Where(duration => duration > 0)
            .Select(duration => (double)duration)
            .ToList();

        if (!durations.Any())
        {
            return null;
        }

        return Math.Round(durations.Average(), 2);
    }

    public static List<double> CalculateMtbfSeconds(IEnumerable<OutageWindow> outages)
    {
        var gaps = new List<double>();
        OutageWindow? previous = null;
        foreach (var outage in outages.OrderBy(o => o.StartedAt))
        {
            if (previous != null && previous.EndedAt.HasValue)
            {
                var gap = (outage.StartedAt - previous.EndedAt.Value).TotalSeconds;
                if (gap > 0)
                {
                    gaps.Add(gap);
                }
            }

            previous = outage;
        }

        return gaps;
    }
}
