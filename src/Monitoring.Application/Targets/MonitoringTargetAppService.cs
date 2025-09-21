using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Monitoring.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Authorization;

namespace Monitoring.Targets;

public class MonitoringTargetAppService :
    CrudAppService<MonitoringTarget, MonitoringTargetDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateMonitoringTargetDto>,
    IMonitoringTargetAppService
{
    public MonitoringTargetAppService(IRepository<MonitoringTarget, Guid> repository)
        : base(repository)
    {
        GetPolicyName = MonitoringPermissions.Services.View;
        GetListPolicyName = MonitoringPermissions.Services.View;
        CreatePolicyName = MonitoringPermissions.Services.Create;
        UpdatePolicyName = MonitoringPermissions.Services.Edit;
        DeletePolicyName = MonitoringPermissions.Services.Delete;
    }

    protected override MonitoringTarget MapToEntity(CreateUpdateMonitoringTargetDto createInput)
    {
        var entity = new MonitoringTarget(
            GuidGenerator.Create(),
            createInput.Name,
            createInput.Type,
            createInput.Endpoint,
            createInput.CheckIntervalSeconds,
            createInput.TimeoutSeconds,
            createInput.MaxRetryAttempts,
            createInput.RetryDelaySeconds,
            createInput.IsActive,
            createInput.CurrentStatus,
            createInput.NextDueAt,
            createInput.SettingsJson,
            createInput.Category
        );

        entity.SetLastCheckedAt(createInput.LastCheckedAt);
        entity.SetLastStatusChangeAt(createInput.LastStatusChangeAt);
        entity.SetConsecutiveFailures(createInput.ConsecutiveFailures);
        entity.SetFirstDownAt(createInput.FirstDownAt);
        entity.SetLastUpAt(createInput.LastUpAt);

        return entity;
    }

    protected override void MapToEntity(CreateUpdateMonitoringTargetDto updateInput, MonitoringTarget entity)
    {
        entity.SetName(updateInput.Name);
        entity.SetType(updateInput.Type);
        entity.SetEndpoint(updateInput.Endpoint);
        entity.UpdateCheckIntervalSeconds(updateInput.CheckIntervalSeconds);
        entity.UpdateTimeoutSeconds(updateInput.TimeoutSeconds);
        entity.UpdateRetrySettings(updateInput.MaxRetryAttempts, updateInput.RetryDelaySeconds);
        entity.SetSettingsJson(updateInput.SettingsJson);
        entity.SetCategory(updateInput.Category);
        entity.UpdateActivation(updateInput.IsActive);
        entity.SetCurrentStatus(updateInput.CurrentStatus);
        entity.SetLastCheckedAt(updateInput.LastCheckedAt);
        entity.SetLastStatusChangeAt(updateInput.LastStatusChangeAt);
        entity.SetNextDueAt(updateInput.NextDueAt);
        entity.SetConsecutiveFailures(updateInput.ConsecutiveFailures);
        entity.SetFirstDownAt(updateInput.FirstDownAt);
        entity.SetLastUpAt(updateInput.LastUpAt);
    }

    public async Task TriggerCheckAsync(Guid id)
    {
        await AuthorizationService.CheckAsync(MonitoringPermissions.Services.Run);

        var entity = await Repository.GetAsync(id);

        var now = Clock.Now;
        entity.SetLastCheckedAt(now);
        entity.SetNextDueAt(now.AddSeconds(entity.CheckIntervalSeconds));
        entity.SetCurrentStatus(ServiceStatus.Checking);
        entity.SetLastStatusChangeAt(now);
        entity.SetConsecutiveFailures(0);

        await Repository.UpdateAsync(entity, autoSave: true);
    }
}
