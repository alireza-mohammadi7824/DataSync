using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Monitoring.Permissions;
using Volo.Abp;

namespace Monitoring.Targets;

[RemoteService(Name = "Monitoring")]
[Area("Monitoring")]
[Authorize(MonitoringPermissions.Services.View)]
[EnableRateLimiting("monitoring-read")]
[Route("api/monitoring/maintenance")]
public class MonitoringMaintenanceController : MonitoringController
{
    private readonly IMonitoringTargetAppService _appService;

    public MonitoringMaintenanceController(IMonitoringTargetAppService appService)
    {
        _appService = appService;
    }

    [HttpGet]
    public virtual Task<List<MaintenanceWindowDto>> GetListAsync([FromQuery] Guid? targetId)
    {
        return _appService.GetMaintenanceAsync(targetId);
    }

    [HttpPost]
    [Authorize(MonitoringPermissions.Services.Edit)]
    [EnableRateLimiting("monitoring-write")]
    public virtual Task<MaintenanceWindowDto> CreateAsync(CreateUpdateMaintenanceWindowDto input)
    {
        return _appService.CreateMaintenanceAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    [Authorize(MonitoringPermissions.Services.Edit)]
    [EnableRateLimiting("monitoring-write")]
    public virtual Task<MaintenanceWindowDto> UpdateAsync(Guid id, CreateUpdateMaintenanceWindowDto input)
    {
        return _appService.UpdateMaintenanceAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    [Authorize(MonitoringPermissions.Services.Edit)]
    [EnableRateLimiting("monitoring-write")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _appService.DeleteMaintenanceAsync(id);
    }
}
