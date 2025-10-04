using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Monitoring.Observability;
using Monitoring.Permissions;
using Volo.Abp;

namespace Monitoring.Controllers;

[RemoteService(Name = "Monitoring")]
[Area("Monitoring")]
[Authorize(MonitoringPermissions.Metrics.View)]
[EnableRateLimiting("monitoring-read")]
[Route("api/monitoring/metrics")]
public class MonitoringMetricsController : MonitoringController
{
    private readonly IMonitoringMetricsAppService _appService;

    public MonitoringMetricsController(IMonitoringMetricsAppService appService)
    {
        _appService = appService;
    }

    [HttpGet]
    public Task<object> GetAsync(CancellationToken cancellationToken)
    {
        return _appService.GetAsync(cancellationToken);
    }
}
