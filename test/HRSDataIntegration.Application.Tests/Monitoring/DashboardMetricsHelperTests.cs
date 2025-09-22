using System;
using System.Collections.Generic;
using Monitoring.Dashboard;
using Monitoring.Targets;
using Xunit;

namespace HRSDataIntegration.Monitoring;

public class DashboardMetricsHelperTests
{
    [Fact]
    public void Calculates_overlap_for_partial_window()
    {
        var outage = new OutageWindow(Guid.NewGuid(), Guid.NewGuid(), new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc), 1);
        outage.Close(new DateTime(2024, 6, 1, 2, 0, 0, DateTimeKind.Utc));

        var overlap = DashboardMetricsHelper.CalculateOverlapSeconds(
            outage,
            new DateTime(2024, 6, 1, 1, 0, 0, DateTimeKind.Utc),
            new DateTime(2024, 6, 1, 3, 0, 0, DateTimeKind.Utc));

        Assert.Equal(3600, overlap);
    }

    [Fact]
    public void Calculates_mttr_in_seconds()
    {
        var outage = new OutageWindow(Guid.NewGuid(), Guid.NewGuid(), new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc), 1);
        outage.Close(new DateTime(2024, 6, 1, 0, 45, 0, DateTimeKind.Utc));

        var mttr = DashboardMetricsHelper.CalculateMttrSeconds(
            new List<OutageWindow> { outage },
            new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2024, 6, 2, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(2700, mttr);
    }

    [Fact]
    public void Calculates_mtbf_gaps_between_outages()
    {
        var targetId = Guid.NewGuid();
        var first = new OutageWindow(Guid.NewGuid(), targetId, new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc), 1);
        first.Close(new DateTime(2024, 6, 1, 0, 10, 0, DateTimeKind.Utc));
        var second = new OutageWindow(Guid.NewGuid(), targetId, new DateTime(2024, 6, 1, 1, 0, 0, DateTimeKind.Utc), 1);
        second.Close(new DateTime(2024, 6, 1, 1, 10, 0, DateTimeKind.Utc));

        var gaps = DashboardMetricsHelper.CalculateMtbfSeconds(new List<OutageWindow> { second, first });

        Assert.Single(gaps);
        Assert.Equal(3000, gaps[0]);
    }
}
