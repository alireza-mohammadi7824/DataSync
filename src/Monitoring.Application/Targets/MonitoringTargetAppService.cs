using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Monitoring.HealthChecks;
using Monitoring.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace Monitoring.Targets;

public class MonitoringTargetAppService :
    CrudAppService<MonitoringTarget, MonitoringTargetDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateMonitoringTargetDto>,
    IMonitoringTargetAppService
{
    private const string ManualTriggerSource = "manual";
    private const string BulkTriggerSource = "api";

    private readonly IHealthCheckProviderResolver _providerResolver;
    private readonly IRepository<ServiceStatusHistory, Guid> _historyRepository;
    private readonly IRepository<OutageWindow, Guid> _outageRepository;
    private readonly IGuidGenerator _guidGenerator;

    public MonitoringTargetAppService(
        IRepository<MonitoringTarget, Guid> repository,
        IHealthCheckProviderResolver providerResolver,
        IRepository<ServiceStatusHistory, Guid> historyRepository,
        IRepository<OutageWindow, Guid> outageRepository,
        IGuidGenerator guidGenerator)
        : base(repository)
    {
        _providerResolver = providerResolver;
        _historyRepository = historyRepository;
        _outageRepository = outageRepository;
        _guidGenerator = guidGenerator;
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
        entity.SetNextDueAt(now);
        entity.SetCurrentStatus(ServiceStatus.Checking);
        entity.SetLastStatusChangeAt(now);
        entity.SetConsecutiveFailures(0);

        await Repository.UpdateAsync(entity, autoSave: true);
    }

    public async Task<HealthCheckResultDto> CheckNowAsync(Guid id)
    {
        await AuthorizationService.CheckAsync(MonitoringPermissions.Services.Run);

        var target = await Repository.GetAsync(id);

        var execution = await ExecuteCheckAsync(target, ManualTriggerSource);
        var result = execution.Result;
        var timestamp = execution.Timestamp;

        await Repository.UpdateAsync(target, autoSave: true);

        return CreateResultDto(target, result, timestamp);
    }

    public async Task<List<HealthCheckResultDto>> CheckAllAsync()
    {
        await AuthorizationService.CheckAsync(MonitoringPermissions.Services.Run);

        var targets = await Repository.GetListAsync(target => target.IsActive);

        var results = new List<HealthCheckResultDto>(targets.Count);

        foreach (var target in targets)
        {
            var execution = await ExecuteCheckAsync(target, BulkTriggerSource);
            await Repository.UpdateAsync(target, autoSave: true);

            results.Add(CreateResultDto(target, execution.Result, execution.Timestamp));
        }

        return results;
    }

    public async Task<int> CheckAllNowAsync()
    {
        await AuthorizationService.CheckAsync(MonitoringPermissions.Services.Run);

        var targets = await Repository.GetListAsync(target => target.IsActive);
        var now = Clock.Now;

        foreach (var target in targets)
        {
            if (target.CurrentStatus != ServiceStatus.Checking)
            {
                target.SetCurrentStatus(ServiceStatus.Checking);
                target.SetLastStatusChangeAt(now);
            }

            target.SetLastCheckedAt(now);
            target.SetNextDueAt(now);
            target.SetConsecutiveFailures(0);

            await Repository.UpdateAsync(target, autoSave: true);
        }

        return targets.Count;
    }

    public async Task<List<MonitoringTargetDto>> GetOverviewAsync(ServiceType? type = null)
    {
        await AuthorizationService.CheckAsync(MonitoringPermissions.Services.View);

        var queryable = await Repository.GetQueryableAsync();

        if (type.HasValue)
        {
            queryable = queryable.Where(target => target.Type == type.Value);
        }

        var targets = await AsyncExecuter.ToListAsync(
            queryable
                .OrderBy(target => target.Name));

        return ObjectMapper.Map<List<MonitoringTarget>, List<MonitoringTargetDto>>(targets);
    }

    public async Task<List<OutageWindowDto>> GetRecentOutagesAsync(Guid targetId, int count = 10)
    {
        await AuthorizationService.CheckAsync(MonitoringPermissions.Services.View);

        var normalizedCount = Math.Clamp(count, 1, 100);
        var queryable = await _outageRepository.GetQueryableAsync();

        var outages = await AsyncExecuter.ToListAsync(
            queryable
                .Where(x => x.TargetId == targetId)
                .OrderByDescending(x => x.StartedAt)
                .Take(normalizedCount));

        return ObjectMapper.Map<List<OutageWindow>, List<OutageWindowDto>>(outages);
    }

    public async Task<List<ServiceStatusHistoryDto>> GetRecentStatusHistoryAsync(Guid targetId, int count = 20)
    {
        await AuthorizationService.CheckAsync(MonitoringPermissions.Services.View);

        var normalizedCount = Math.Clamp(count, 1, 200);
        var queryable = await _historyRepository.GetQueryableAsync();

        var history = await AsyncExecuter.ToListAsync(
            queryable
                .Where(x => x.TargetId == targetId)
                .OrderByDescending(x => x.ChangedAt)
                .Take(normalizedCount));

        return ObjectMapper.Map<List<ServiceStatusHistory>, List<ServiceStatusHistoryDto>>(history);
    }

    private async Task<(HealthCheckResult Result, DateTime Timestamp)> ExecuteCheckAsync(
        MonitoringTarget target,
        string triggerSource,
        CancellationToken cancellationToken = default)
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
        await MonitoringTargetCheckProcessor.ApplyResultAsync(
            target,
            result,
            timestamp,
            _historyRepository,
            _outageRepository,
            _guidGenerator,
            cancellationToken);

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
