using System;
using System.Threading.Tasks;
using Monitoring.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace Monitoring.Tasks;

public class MonitoringTaskAppService :
    CrudAppService<MonitoringTask, MonitoringTaskDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateMonitoringTaskDto>,
    IMonitoringTaskAppService
{
    public MonitoringTaskAppService(IRepository<MonitoringTask, Guid> repository)
        : base(repository)
    {
        GetPolicyName = MonitoringPermissions.View;
        GetListPolicyName = MonitoringPermissions.View;
        CreatePolicyName = MonitoringPermissions.Create;
        UpdatePolicyName = MonitoringPermissions.Edit;
        DeletePolicyName = MonitoringPermissions.Delete;
    }

    protected override MonitoringTask MapToEntity(CreateUpdateMonitoringTaskDto createInput)
    {
        return new MonitoringTask(
            GuidGenerator.Create(),
            createInput.Name,
            createInput.TargetUrl,
            createInput.IsActive,
            createInput.CheckIntervalInSeconds,
            createInput.AuthenticationSecretRef
        );
    }

    protected override void MapToEntity(CreateUpdateMonitoringTaskDto updateInput, MonitoringTask entity)
    {
        entity.SetName(updateInput.Name);
        entity.SetTargetUrl(updateInput.TargetUrl);
        entity.UpdateActivation(updateInput.IsActive);
        entity.UpdateCheckInterval(updateInput.CheckIntervalInSeconds);
        entity.SetAuthenticationSecretRef(updateInput.AuthenticationSecretRef);
    }

    public async Task TriggerExecutionAsync(Guid id)
    {
        await AuthorizationService.CheckAsync(MonitoringPermissions.Run);

        var entity = await Repository.GetAsync(id);

        entity.ReportExecution(Clock.Now, succeeded: true);

        await Repository.UpdateAsync(entity, autoSave: true);
    }
}
