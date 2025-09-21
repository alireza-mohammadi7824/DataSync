using System;
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
}
