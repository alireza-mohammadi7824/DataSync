using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitoring.Permissions;
using Volo.Abp;

namespace Monitoring.Targets;

[RemoteService(Name = "Monitoring")]
[Area("Monitoring")]
[Authorize(MonitoringPermissions.Services.View)]
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
    public virtual Task<MaintenanceWindowDto> CreateAsync(CreateUpdateMaintenanceWindowDto input)
    {
        return _appService.CreateMaintenanceAsync(input);
    }

    [HttpDelete]
    [Route("{id}")]
    [Authorize(MonitoringPermissions.Services.Edit)]
    public virtual Task DeleteAsync(Guid id)
    {
        return _appService.DeleteMaintenanceAsync(id);
    }
}
