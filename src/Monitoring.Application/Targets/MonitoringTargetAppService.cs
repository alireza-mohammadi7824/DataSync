using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Monitoring.HealthChecks;
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
    private const string ManualTriggerSource = "manual";
    private const string BulkTriggerSource = "api";

    private readonly IHealthCheckProviderResolver _providerResolver;

    public MonitoringTargetAppService(
        IRepository<MonitoringTarget, Guid> repository,
        IHealthCheckProviderResolver providerResolver)
        : base(repository)
    {
        _providerResolver = providerResolver;
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

        var cancellationToken = CancellationTokenProvider.Token;
        var entity = await Repository.GetAsync(id, cancellationToken: cancellationToken);

        var now = Clock.Now;
        entity.SetLastCheckedAt(now);
        entity.SetNextDueAt(now.AddSeconds(entity.CheckIntervalSeconds));
        entity.SetCurrentStatus(ServiceStatus.Checking);
        entity.SetLastStatusChangeAt(now);
        entity.SetConsecutiveFailures(0);

        await Repository.UpdateAsync(entity, autoSave: true, cancellationToken: cancellationToken);
    }

    public async Task<HealthCheckResultDto> CheckNowAsync(Guid id)
    {
        await AuthorizationService.CheckAsync(MonitoringPermissions.Services.Run);

        var cancellationToken = CancellationTokenProvider.Token;
        var target = await Repository.GetAsync(id, cancellationToken: cancellationToken);

        var (result, timestamp) = await ExecuteCheckAsync(target, ManualTriggerSource, cancellationToken);

        await Repository.UpdateAsync(target, autoSave: true, cancellationToken: cancellationToken);

        return CreateResultDto(target, result, timestamp);
    }

    public async Task<List<HealthCheckResultDto>> CheckAllAsync()
    {
        await AuthorizationService.CheckAsync(MonitoringPermissions.Services.Run);

        var cancellationToken = CancellationTokenProvider.Token;
        var targets = await Repository.GetListAsync(target => target.IsActive, cancellationToken: cancellationToken);

        var results = new List<HealthCheckResultDto>(targets.Count);

        foreach (var target in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (result, timestamp) = await ExecuteCheckAsync(target, BulkTriggerSource, cancellationToken);
            await Repository.UpdateAsync(target, autoSave: true, cancellationToken: cancellationToken);

            results.Add(CreateResultDto(target, result, timestamp));
        }

        return results;
    }

    private async Task<(HealthCheckResult Result, DateTime Timestamp)> ExecuteCheckAsync(
        MonitoringTarget target,
        string triggerSource,
        CancellationToken cancellationToken)
    {
        HealthCheckResult result;
        try
        {
            var provider = _providerResolver.Resolve(target.Type);
            result = await provider.CheckAsync(target, triggerSource, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Monitoring check failed for target {TargetId}", target.Id);
            result = new HealthCheckResult(false, null, "Check error", triggerSource);
        }

        var timestamp = Clock.Now;
        MonitoringTargetCheckProcessor.ApplyResult(target, result, timestamp);

        return (result, timestamp);
    }

    private static HealthCheckResultDto CreateResultDto(
        MonitoringTarget target,
        HealthCheckResult result,
        DateTime timestamp)
    {
        return new HealthCheckResultDto
        {
            TargetId = target.Id,
            Status = target.CurrentStatus,
            ResponseTimeMs = result.ResponseTimeMs,
            ErrorSummary = result.ErrorSummary,
            CheckedAt = timestamp
        };
    }
}
