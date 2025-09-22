using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Monitoring.Dashboard;
using Monitoring.Permissions;

namespace Monitoring.Controllers;

[Route("api/monitoring/dashboard")]
[Authorize(MonitoringPermissions.Dashboard.View)]
[EnableRateLimiting("monitoring-read")]
public class MonitoringDashboardController : MonitoringController
{
    private readonly IDashboardAppService _dashboardAppService;

    public MonitoringDashboardController(IDashboardAppService dashboardAppService)
    {
        _dashboardAppService = dashboardAppService;
    }

    [HttpGet("summary")]
    public Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken)
    {
        return _dashboardAppService.GetSummaryAsync(cancellationToken);
    }

    [HttpGet("targets")]
    public Task<TargetDashboardListDto> GetTargetsAsync([FromQuery] TargetDashboardListInput input, CancellationToken cancellationToken)
    {
        return _dashboardAppService.GetTargetsAsync(input, cancellationToken);
    }
}
