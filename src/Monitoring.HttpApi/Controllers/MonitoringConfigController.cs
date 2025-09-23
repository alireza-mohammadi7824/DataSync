using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Monitoring.Config;
using Monitoring.Permissions;

namespace Monitoring.Controllers;

[Route("api/monitoring/config")]
[Authorize(MonitoringPermissions.Config.View)]
[EnableRateLimiting("monitoring-read")]
public sealed class MonitoringConfigController : MonitoringController
{
    private readonly IMonitoringConfigAppService _configAppService;

    public MonitoringConfigController(IMonitoringConfigAppService configAppService)
    {
        _configAppService = configAppService;
    }

    [HttpGet]
    public Task<MonitoringConfigDto> GetAsync()
    {
        return _configAppService.GetAsync();
    }
}
