using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Monitoring.Permissions;

namespace Monitoring.Tasks;

[RemoteService(Name = "Monitoring")]
[Area("Monitoring")]
[Authorize(MonitoringPermissions.View)]
[Route("api/monitoring/tasks")]
public class MonitoringTaskController : MonitoringController
{
    private readonly IMonitoringTaskAppService _monitoringTaskAppService;

    public MonitoringTaskController(IMonitoringTaskAppService monitoringTaskAppService)
    {
        _monitoringTaskAppService = monitoringTaskAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<MonitoringTaskDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        return _monitoringTaskAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<MonitoringTaskDto> GetAsync(Guid id)
    {
        return _monitoringTaskAppService.GetAsync(id);
    }

    [HttpPost]
    public virtual Task<MonitoringTaskDto> CreateAsync(CreateUpdateMonitoringTaskDto input)
    {
        return _monitoringTaskAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<MonitoringTaskDto> UpdateAsync(Guid id, CreateUpdateMonitoringTaskDto input)
    {
        return _monitoringTaskAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _monitoringTaskAppService.DeleteAsync(id);
    }

    [HttpPost]
    [Route("{id}/trigger")]
    [Authorize(MonitoringPermissions.Run)]
    public virtual Task TriggerAsync(Guid id)
    {
        return _monitoringTaskAppService.TriggerExecutionAsync(id);
    }
}
