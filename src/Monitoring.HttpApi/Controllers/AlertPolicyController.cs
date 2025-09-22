using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Monitoring.Alerts;
using Monitoring.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Http.Modeling;

namespace Monitoring.Alerts;

[RemoteService(Name = "Monitoring")]
[Area("Monitoring")]
[Route("api/monitoring/alerts/policies")]
public class AlertPolicyController : MonitoringController
{
    private readonly IAlertPolicyAppService _appService;

    public AlertPolicyController(IAlertPolicyAppService appService)
    {
        _appService = appService;
    }

    [HttpGet]
    [Authorize(MonitoringPermissions.AlertPolicies.View)]
    public Task<PagedResultDto<AlertPolicyDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        return _appService.GetListAsync(input);
    }

    [HttpGet]
    [Route("{id}")]
    [Authorize(MonitoringPermissions.AlertPolicies.View)]
    public Task<AlertPolicyDto> GetAsync(Guid id)
    {
        return _appService.GetAsync(id);
    }

    [HttpPost]
    [Authorize(MonitoringPermissions.AlertPolicies.Create)]
    [EnableRateLimiting("monitoring-write")]
    public Task<AlertPolicyDto> CreateAsync(CreateUpdateAlertPolicyDto input)
    {
        return _appService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    [Authorize(MonitoringPermissions.AlertPolicies.Edit)]
    [EnableRateLimiting("monitoring-write")]
    public Task<AlertPolicyDto> UpdateAsync(Guid id, CreateUpdateAlertPolicyDto input)
    {
        return _appService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    [Authorize(MonitoringPermissions.AlertPolicies.Delete)]
    [EnableRateLimiting("monitoring-write")]
    public Task DeleteAsync(Guid id)
    {
        return _appService.DeleteAsync(id);
    }
}
