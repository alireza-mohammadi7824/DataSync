using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitoring.Dashboard;
using Monitoring.Targets;
using Monitoring.Permissions;

namespace Monitoring.Controllers;

[Route("api/monitoring/dashboard")]
[Authorize(MonitoringPermissions.Services.View)]
public class MonitoringDashboardController : MonitoringController
{
    private readonly IDashboardAppService _dashboardAppService;

    public MonitoringDashboardController(IDashboardAppService dashboardAppService)
    {
        _dashboardAppService = dashboardAppService;
    }

    [HttpGet("summary")]
    public Task<DashboardSummaryDto> GetSummaryAsync([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] ServiceType? type)
    {
        return _dashboardAppService.GetSummaryAsync(from ?? default, to ?? default, type);
    }

    [HttpGet("uptime/{id}")]
    public Task<List<UptimeBucketDto>> GetUptimeAsync(Guid id, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string bucket = "day")
    {
        return _dashboardAppService.GetUptimeSeriesAsync(id, from ?? default, to ?? default, bucket);
    }

    [HttpGet("incidents/{id}")]
    public Task<List<DashboardIncidentDto>> GetIncidentsAsync(
        Guid id,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int skip = 0,
        [FromQuery] int max = 100)
    {
        return _dashboardAppService.GetIncidentsAsync(id, from ?? default, to ?? default, skip, max);
    }

    [HttpGet("mttr-mtbf")]
    public Task<MttrMtbfDto> GetReliabilityAsync([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] ServiceType? type)
    {
        return _dashboardAppService.GetReliabilityAsync(from ?? default, to ?? default, type);
    }
}
