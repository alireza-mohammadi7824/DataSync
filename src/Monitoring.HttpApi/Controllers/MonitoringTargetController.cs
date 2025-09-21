using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Monitoring.Permissions;

namespace Monitoring.Targets;

[RemoteService(Name = "Monitoring")]
[Area("Monitoring")]
[Authorize(MonitoringPermissions.Services.View)]
[Route("api/monitoring/targets")]
public class MonitoringTargetController : MonitoringController
{
    private readonly IMonitoringTargetAppService _monitoringTargetAppService;

    public MonitoringTargetController(IMonitoringTargetAppService monitoringTargetAppService)
    {
        _monitoringTargetAppService = monitoringTargetAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<MonitoringTargetDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        return _monitoringTargetAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<MonitoringTargetDto> GetAsync(Guid id)
    {
        return _monitoringTargetAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("{id}/outages")]
    public virtual Task<List<OutageWindowDto>> GetRecentOutagesAsync(Guid id, [FromQuery] int count = 10)
    {
        return _monitoringTargetAppService.GetRecentOutagesAsync(id, count);
    }

    [HttpGet]
    [Route("{id}/history")]
    public virtual Task<List<ServiceStatusHistoryDto>> GetRecentStatusHistoryAsync(Guid id, [FromQuery] int count = 20)
    {
        return _monitoringTargetAppService.GetRecentStatusHistoryAsync(id, count);
    }

    [HttpPost]
    [Authorize(MonitoringPermissions.Services.Create)]
    public virtual Task<MonitoringTargetDto> CreateAsync(CreateUpdateMonitoringTargetDto input)
    {
        return _monitoringTargetAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    [Authorize(MonitoringPermissions.Services.Edit)]
    public virtual Task<MonitoringTargetDto> UpdateAsync(Guid id, CreateUpdateMonitoringTargetDto input)
    {
        return _monitoringTargetAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    [Authorize(MonitoringPermissions.Services.Delete)]
    public virtual Task DeleteAsync(Guid id)
    {
        return _monitoringTargetAppService.DeleteAsync(id);
    }

    [HttpPost]
    [Route("{id}/trigger")]
    [Authorize(MonitoringPermissions.Services.Run)]
    public virtual Task TriggerAsync(Guid id)
    {
        return _monitoringTargetAppService.TriggerCheckAsync(id);
    }

    [HttpPost]
    [Route("{id}/check")]
    [Authorize(MonitoringPermissions.Services.Run)]
    public virtual Task<HealthCheckResultDto> CheckNowAsync(Guid id)
    {
        return _monitoringTargetAppService.CheckNowAsync(id);
    }

    [HttpPost]
    [Route("check-all")]
    [Authorize(MonitoringPermissions.Services.Run)]
    public virtual Task<List<HealthCheckResultDto>> CheckAllAsync()
    {
        return _monitoringTargetAppService.CheckAllAsync();
    }

    [HttpPost]
    [Route("~/api/monitoring/check-all")]
    [Authorize(MonitoringPermissions.Services.Run)]
    public virtual Task<int> CheckAllNowAsync()
    {
        return _monitoringTargetAppService.CheckAllNowAsync();
    }
}
