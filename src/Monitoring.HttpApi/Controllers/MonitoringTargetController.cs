using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Monitoring.Execution;
using Monitoring.History;
using Volo.Abp.Http;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Validation;
using Monitoring.Permissions;

namespace Monitoring.Targets;

[RemoteService(Name = "Monitoring")]
[Area("Monitoring")]
[Authorize(MonitoringPermissions.Services.View)]
[EnableRateLimiting("monitoring-read")]
[Route("api/monitoring/targets")]
public class MonitoringTargetController : MonitoringController
{
    private readonly IMonitoringTargetAppService _monitoringTargetAppService;
    private readonly IHistoryAppService _historyAppService;

    public MonitoringTargetController(
        IMonitoringTargetAppService monitoringTargetAppService,
        IHistoryAppService historyAppService)
    {
        _monitoringTargetAppService = monitoringTargetAppService;
        _historyAppService = historyAppService;
    }

    [HttpGet]
    [Route("overview")]
    public virtual Task<List<MonitoringTargetDto>> GetOverviewAsync([FromQuery] ServiceType? type)
    {
        return _monitoringTargetAppService.GetOverviewAsync(type);
    }

    [HttpGet]
    public virtual Task<PagedResultDto<MonitoringTargetDto>> GetListAsync(
        PagedAndSortedResultRequestDto input,
        [FromQuery] ServiceType? type,
        [FromQuery] string? search)
    {
        return _monitoringTargetAppService.GetListAsync(input, type, search);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<MonitoringTargetDto> GetAsync(Guid id)
    {
        return _monitoringTargetAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("{id}/outages")]
    public virtual Task<OutageListDto> GetOutagesAsync(Guid id, [FromQuery] int? count, CancellationToken cancellationToken)
    {
        return _historyAppService.GetOutagesAsync(id, count, cancellationToken);
    }

    [HttpGet]
    [Route("{id}/alert-policy")]
    public virtual Task<AlertPolicyDto> GetAlertPolicyAsync(Guid id)
    {
        return _monitoringTargetAppService.GetAlertPolicyAsync(id);
    }

    [HttpPut]
    [Route("{id}/alert-policy")]
    [Authorize(MonitoringPermissions.Services.Edit)]
    [EnableRateLimiting("monitoring-write")]
    public virtual Task<AlertPolicyDto> UpsertAlertPolicyAsync(Guid id, AlertPolicyDto input)
    {
        return _monitoringTargetAppService.UpsertAlertPolicyAsync(id, input);
    }

    [HttpGet]
    [Route("{id}/timeline")]
    public virtual Task<TimelineDto> GetTimelineAsync(Guid id, [FromQuery] TimelineRequestDto? input, CancellationToken cancellationToken)
    {
        if (input == null)
        {
            throw new AbpValidationException("Timeline request is required.");
        }

        return _historyAppService.GetTimelineAsync(id, input, cancellationToken);
    }

    [HttpPost]
    [Authorize(MonitoringPermissions.Services.Create)]
    [EnableRateLimiting("monitoring-write")]
    public virtual Task<MonitoringTargetDto> CreateAsync(CreateUpdateMonitoringTargetDto input)
    {
        return _monitoringTargetAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    [Authorize(MonitoringPermissions.Services.Edit)]
    [EnableRateLimiting("monitoring-write")]
    public virtual Task<MonitoringTargetDto> UpdateAsync(Guid id, CreateUpdateMonitoringTargetDto input)
    {
        return _monitoringTargetAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    [Authorize(MonitoringPermissions.Services.Delete)]
    [EnableRateLimiting("monitoring-write")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _monitoringTargetAppService.DeleteAsync(id);
    }

    [HttpPost]
    [Route("{id}/trigger")]
    [Authorize(MonitoringPermissions.Services.Run)]
    [EnableRateLimiting("monitoring-write")]
    public virtual async Task<IActionResult> TriggerAsync(Guid id)
    {
        try
        {
            await _monitoringTargetAppService.TriggerCheckAsync(id);
            return Accepted();
        }
        catch (MonitoringCheckConflictException ex)
        {
            return Conflict(new RemoteServiceErrorInfo(ex.Message));
        }
    }

    [HttpPost]
    [Route("{id}/check")]
    [Authorize(MonitoringPermissions.Services.Run)]
    [EnableRateLimiting("monitoring-write")]
    public virtual async Task<IActionResult> CheckNowAsync(Guid id)
    {
        try
        {
            var dto = await _monitoringTargetAppService.CheckNowAsync(id);
            return Ok(dto);
        }
        catch (MonitoringCheckConflictException ex)
        {
            return Conflict(new RemoteServiceErrorInfo(ex.Message));
        }
    }

    [HttpPost]
    [Route("check-all")]
    [Authorize(MonitoringPermissions.Services.Run)]
    [EnableRateLimiting("monitoring-write")]
    public virtual async Task<IActionResult> EnqueueCheckAllAsync()
    {
        var result = await _monitoringTargetAppService.EnqueueCheckAllAsync();
        return AcceptedAtAction(nameof(GetCheckBatchStatusAsync), new { id = result.BatchId }, result);
    }

    [HttpGet]
    [Route("~/api/monitoring/check-batch/{id}/status")]
    public virtual Task<CheckBatchStatusDto> GetCheckBatchStatusAsync(Guid id)
    {
        return _monitoringTargetAppService.GetCheckBatchStatusAsync(id);
    }

}
