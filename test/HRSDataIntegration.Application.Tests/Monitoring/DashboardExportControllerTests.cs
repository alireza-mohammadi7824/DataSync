using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Monitoring.Controllers;
using Monitoring.Dashboard;
using Monitoring.Targets;
using Xunit;

namespace HRSDataIntegration.Monitoring;

public class DashboardExportControllerTests
{
    [Fact]
    public async Task Export_uptime_generates_csv()
    {
        var service = new FakeDashboardAppService();
        var controller = new MonitoringExportController(service);

        var result = await controller.ExportUptimeAsync(Guid.NewGuid(), DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, "day");

        Assert.Equal("text/csv", result.ContentType);
        var content = Encoding.UTF8.GetString(result.FileContents);
        Assert.StartsWith("start,end,uptimePercentage", content);
        Assert.Contains("100", content);
    }

    [Fact]
    public async Task Export_incidents_generates_csv()
    {
        var service = new FakeDashboardAppService();
        var controller = new MonitoringExportController(service);

        var result = await controller.ExportIncidentsAsync(Guid.NewGuid(), DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);

        Assert.Equal("text/csv", result.ContentType);
        var content = Encoding.UTF8.GetString(result.FileContents);
        Assert.Contains("durationSeconds", content);
        Assert.Contains("failureCount", content);
    }

    private sealed class FakeDashboardAppService : IDashboardAppService
    {
        public Task<List<DashboardIncidentDto>> GetIncidentsAsync(Guid targetId, DateTime from, DateTime to, int skip = 0, int max = 100)
        {
            return Task.FromResult(new List<DashboardIncidentDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    TargetId = targetId,
                    StartedAt = from,
                    EndedAt = from.AddMinutes(5),
                    FailureCount = 3,
                    TotalDurationSec = 300
                }
            });
        }

        public Task<MttrMtbfDto> GetReliabilityAsync(DateTime from, DateTime to, ServiceType? filterType = null)
        {
            return Task.FromResult(new MttrMtbfDto());
        }

        public Task<DashboardSummaryDto> GetSummaryAsync(DateTime from, DateTime to, ServiceType? filterType = null)
        {
            return Task.FromResult(new DashboardSummaryDto());
        }

        public Task<List<UptimeBucketDto>> GetUptimeSeriesAsync(Guid targetId, DateTime from, DateTime to, string bucket = "day")
        {
            return Task.FromResult(new List<UptimeBucketDto>
            {
                new()
                {
                    Start = from,
                    End = to,
                    UptimePercentage = 100
                }
            });
        }
    }
}
