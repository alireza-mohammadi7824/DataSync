using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Monitoring.Execution;
using Monitoring.Endpoints;
using Monitoring.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Threading;

namespace Monitoring.Targets;

public class MonitoringTargetAppService : ApplicationService, IMonitoringTargetAppService
{
    private const int DefaultPageSize = 12;
    private const int MaxPageSize = 100;

    private static readonly JsonSerializerOptions SettingsSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IRepository<MonitoringTarget, Guid> _repository;
    private readonly IRepository<ServiceStatusHistory, Guid> _historyRepository;
    private readonly IRepository<OutageWindow, Guid> _outageRepository;
    private readonly IRepository<MaintenanceWindow, Guid> _maintenanceRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IMonitoringCheckService _checkService;
    private readonly IBulkCheckQueue _bulkCheckQueue;
    private readonly ExecutionMetrics _metrics;
    private readonly ICancellationTokenProvider _cancellationTokenProvider;

    public MonitoringTargetAppService(
        IRepository<MonitoringTarget, Guid> repository,
        IRepository<ServiceStatusHistory, Guid> historyRepository,
        IRepository<OutageWindow, Guid> outageRepository,
        IRepository<MaintenanceWindow, Guid> maintenanceRepository,
        IGuidGenerator guidGenerator,
        IMonitoringCheckService checkService,
        IBulkCheckQueue bulkCheckQueue,
        ExecutionMetrics metrics,
        ICancellationTokenProvider cancellationTokenProvider)
    {
        _repository = repository;
        _historyRepository = historyRepository;
        _outageRepository = outageRepository;
        _maintenanceRepository = maintenanceRepository;
        _guidGenerator = guidGenerator;
        _checkService = checkService;
        _bulkCheckQueue = bulkCheckQueue;
        _metrics = metrics;
        _cancellationTokenProvider = cancellationTokenProvider;
    }

    public async Task<PagedResultDto<MonitoringTargetDto>> GetListAsync(
        PagedAndSortedResultRequestDto input,
        ServiceType? type = null,
        string? search = null)
    {
        await AuthorizationService.CheckAsync(MonitoringPermissions.Services.View);

        var maxResultCount = input.MaxResultCount <= 0
            ? DefaultPageSize
            : Math.Min(input.MaxResultCount, MaxPageSize);
        var skipCount = input.SkipCount < 0 ? 0 : input.SkipCount;

        var queryable = await _repository.GetQueryableAsync();

        if (type.HasValue)
        {
            queryable = queryable.Where(target => target.Type == type.Value);
        }

        if (!search.IsNullOrWhiteSpace())
        {
            var term = search!.Trim();
            queryable = queryable.Where(target => target.Name.Contains(term) || target.Endpoint.Contains(term));
        }

        queryable = ApplySorting(queryable, input.Sorting);

        var totalCount = await AsyncExecuter.CountAsync(queryable);
        var items = await AsyncExecuter.ToListAsync(
            queryable
                .Skip(skipCount)
                .Take(maxResultCount));

        var dtos = ObjectMapper.Map<List<MonitoringTarget>, List<MonitoringTargetDto>>(items);

        if (dtos.Count > 0)
        {
            var now = Clock.Now;
            var targetIds = dtos.Select(x => x.Id).ToList();
            var maintenanceQueryable = await _maintenanceRepository.GetQueryableAsync();
            var activeWindows = await AsyncExecuter.ToListAsync(
                maintenanceQueryable
                    .Where(window => window.StartUtc <= now && window.EndUtc >= now)
                    .Where(window => window.TargetId == null || targetIds.Contains(window.TargetId.Value)));

            var hasGlobal = activeWindows.Any(window => window.TargetId == null);
            var targeted = new HashSet<Guid>(activeWindows.Where(window => window.TargetId.HasValue).Select(window => window.TargetId!.Value));

            foreach (var dto in dtos)
            {
                dto.HasActiveMaintenance = hasGlobal || targeted.Contains(dto.Id);
            }
        }

        return new PagedResultDto<MonitoringTargetDto>(totalCount, dtos);
    }

    public async Task<MonitoringTargetDto> GetAsync(Guid id)
    {
        await AuthorizationService.CheckAsync(MonitoringPermissions.Services.View);
        var entity = await _repository.GetAsync(id);
        var dto = ObjectMapper.Map<MonitoringTarget, MonitoringTargetDto>(entity);

        var now = Clock.Now;
        dto.HasActiveMaintenance = await _maintenanceRepository.AnyAsync(
            window => window.StartUtc <= now && window.EndUtc >= now &&
                      (window.TargetId == null || window.TargetId == id));

        return dto;
    }

    public async Task<MonitoringTargetDto> CreateAsync(CreateUpdateMonitoringTargetDto input)
    {
        await AuthorizationService.CheckAsync(MonitoringPermissions.Services.Create);
        ValidateInput(input);

        var now = Clock.Now;
        var entity = new MonitoringTarget(
            _guidGenerator.Create(),
            input.Name,
            input.Type,
            input.Endpoint,
            input.CheckIntervalSeconds,
            input.TimeoutSeconds,
            input.MaxRetryAttempts,
            input.RetryDelaySeconds,
            input.IsActive,
            ServiceStatus.Checking,
            now,
            input.SettingsJson,
            input.Category);

        entity.SetConsecutiveFailures(0);
        entity.SetLastCheckedAt(null);
        entity.SetLastStatusChangeAt(now);
        entity.SetFirstDownAt(null);
        entity.SetLastUpAt(null);

        var created = await _repository.InsertAsync(entity, autoSave: true);
        return ObjectMapper.Map<MonitoringTarget, MonitoringTargetDto>(created);
    }

    public async Task<MonitoringTargetDto> UpdateAsync(Guid id, CreateUpdateMonitoringTargetDto input)
    {
        await AuthorizationService.CheckAsync(MonitoringPermissions.Services.Edit);
        ValidateInput(input);

        var entity = await _repository.GetAsync(id);
        var typeChanged = entity.Type != input.Type;

        entity.SetName(input.Name);
        entity.SetType(input.Type);
        entity.SetEndpoint(input.Endpoint);
        entity.UpdateCheckIntervalSeconds(input.CheckIntervalSeconds);
        entity.UpdateTimeoutSeconds(input.TimeoutSeconds);
        entity.UpdateRetrySettings(input.MaxRetryAttempts, input.RetryDelaySeconds);
        entity.SetSettingsJson(input.SettingsJson);
        entity.SetCategory(input.Category);
        entity.UpdateActivation(input.IsActive);

        if (typeChanged)
        {
            entity.SetCurrentStatus(ServiceStatus.Checking);
            entity.SetConsecutiveFailures(0);
            entity.SetFirstDownAt(null);
            entity.SetLastStatusChangeAt(Clock.Now);
        }

        entity.SetNextDueAt(Clock.Now.AddSeconds(input.CheckIntervalSeconds));

        var updated = await _repository.UpdateAsync(entity, autoSave: true);
        return ObjectMapper.Map<MonitoringTarget, MonitoringTargetDto>(updated);
    }

    public async Task DeleteAsync(Guid id)
    {
        await AuthorizationService.CheckAsync(MonitoringPermissions.Services.Delete);
        await _repository.DeleteAsync(id);
    }

    public async Task TriggerCheckAsync(Guid id)
    {
        await AuthorizationService.CheckAsync(MonitoringPermissions.Services.Run);

        var target = await _repository.GetAsync(id);
        var execution = await _checkService.RunAsync(target, "manual-trigger", true, _cancellationTokenProvider.Token);

        if (execution.IsSkipped)
        {
            throw new MonitoringCheckConflictException(execution.SkipReason ?? "Check skipped");
        }
    }

    public async Task<HealthCheckResultDto> CheckNowAsync(Guid id)
    {
        await AuthorizationService.CheckAsync(MonitoringPermissions.Services.Run);

        var target = await _repository.GetAsync(id);
        var execution = await _checkService.RunAsync(target, "manual", true, _cancellationTokenProvider.Token);

        if (execution.IsSkipped)
        {
            throw new MonitoringCheckConflictException(execution.SkipReason ?? "Check skipped");
        }

        return CreateResultDto(target, execution.Result!, execution.CompletedAt!.Value);
    }

    public async Task<CheckBatchEnqueueResultDto> EnqueueCheckAllAsync()
    {
        await AuthorizationService.CheckAsync(MonitoringPermissions.Services.Run);

        var queryable = await _repository.GetQueryableAsync();
        var ids = await AsyncExecuter.ToListAsync(
            queryable
                .Where(target => target.IsActive)
                .Select(target => target.Id),
            _cancellationTokenProvider.Token);

        var batchId = _bulkCheckQueue.Enqueue(ids);

        return new CheckBatchEnqueueResultDto
        {
            BatchId = batchId
        };
    }

    public async Task<CheckBatchStatusDto> GetCheckBatchStatusAsync(Guid batchId)
    {
        await AuthorizationService.CheckAsync(MonitoringPermissions.Services.View);

        var status = _bulkCheckQueue.GetStatus(batchId);
        return new CheckBatchStatusDto
        {
            BatchId = status.BatchId,
            TotalTargets = status.Total,
            Queued = status.Queued,
            Running = status.Running,
            Completed = status.Completed,
            Failed = status.Failed
        };
    }

    public async Task<MonitoringMetricsDto> GetMetricsAsync()
    {
        await AuthorizationService.CheckAsync(MonitoringPermissions.Services.View);

        var snapshot = _metrics.CreateSnapshot();
        return new MonitoringMetricsDto
        {
            ChecksStarted = snapshot.ChecksStarted,
            ChecksSucceeded = snapshot.ChecksSucceeded,
            ChecksFailed = snapshot.ChecksFailed,
            ChecksSkipped = snapshot.ChecksSkipped,
            LocksContended = snapshot.LocksContended,
            PurgeSummaries = snapshot.RecentPurges
                .Select(p => new PurgeSummaryDto
                {
                    CompletedAt = p.CompletedAt,
                    HistoryRemoved = p.HistoryRemoved,
                    OutagesRemoved = p.OutagesRemoved
                })
                .ToList()
        };
    }

    public async Task<List<MonitoringTargetDto>> GetOverviewAsync(ServiceType? type = null)
    {
        await AuthorizationService.CheckAsync(MonitoringPermissions.Services.View);

        var queryable = await _repository.GetQueryableAsync();

        if (type.HasValue)
        {
            queryable = queryable.Where(target => target.Type == type.Value);
        }

        var targets = await AsyncExecuter.ToListAsync(
            queryable
                .OrderBy(target => target.Name));

        var dtos = ObjectMapper.Map<List<MonitoringTarget>, List<MonitoringTargetDto>>(targets);

        if (dtos.Count > 0)
        {
            var now = Clock.Now;
            var ids = dtos.Select(x => x.Id).ToList();
            var maintenanceQueryable = await _maintenanceRepository.GetQueryableAsync();
            var activeWindows = await AsyncExecuter.ToListAsync(
                maintenanceQueryable
                    .Where(window => window.StartUtc <= now && window.EndUtc >= now)
                    .Where(window => window.TargetId == null || ids.Contains(window.TargetId.Value)));

            var hasGlobal = activeWindows.Any(window => window.TargetId == null);
            var targeted = new HashSet<Guid>(activeWindows.Where(window => window.TargetId.HasValue).Select(window => window.TargetId!.Value));

            foreach (var dto in dtos)
            {
                dto.HasActiveMaintenance = hasGlobal || targeted.Contains(dto.Id);
            }
        }

        return dtos;
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

    public async Task<List<MaintenanceWindowDto>> GetMaintenanceAsync(Guid? targetId = null)
    {
        await AuthorizationService.CheckAsync(MonitoringPermissions.Services.View);

        var queryable = await _maintenanceRepository.GetQueryableAsync();

        if (targetId.HasValue)
        {
            var id = targetId.Value;
            queryable = queryable.Where(x => x.TargetId == null || x.TargetId == id);
        }

        var windows = await AsyncExecuter.ToListAsync(
            queryable
                .OrderByDescending(x => x.StartUtc));

        return ObjectMapper.Map<List<MaintenanceWindow>, List<MaintenanceWindowDto>>(windows);
    }

    public async Task<MaintenanceWindowDto> CreateMaintenanceAsync(CreateUpdateMaintenanceWindowDto input)
    {
        await AuthorizationService.CheckAsync(MonitoringPermissions.Services.Edit);

        if (input == null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        ValidateMaintenanceWindow(input);

        var targetId = input.IsGlobal ? (Guid?)null : input.TargetId;

        if (targetId.HasValue)
        {
            await EnsureTargetExistsAsync(targetId.Value);
        }

        var entity = new MaintenanceWindow(
            _guidGenerator.Create(),
            targetId,
            input.StartUtc,
            input.EndUtc,
            input.Reason,
            input.RecordButDontAlert,
            Clock.Now);

        var created = await _maintenanceRepository.InsertAsync(entity, autoSave: true);
        return ObjectMapper.Map<MaintenanceWindow, MaintenanceWindowDto>(created);
    }

    public async Task<MaintenanceWindowDto> UpdateMaintenanceAsync(Guid id, CreateUpdateMaintenanceWindowDto input)
    {
        await AuthorizationService.CheckAsync(MonitoringPermissions.Services.Edit);

        if (input == null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        ValidateMaintenanceWindow(input);

        var entity = await _maintenanceRepository.GetAsync(id);

        var targetId = input.IsGlobal ? (Guid?)null : input.TargetId;
        if (targetId.HasValue)
        {
            await EnsureTargetExistsAsync(targetId.Value);
        }

        entity.Update(targetId, input.StartUtc, input.EndUtc, input.Reason, input.RecordButDontAlert);

        await _maintenanceRepository.UpdateAsync(entity, autoSave: true);

        return ObjectMapper.Map<MaintenanceWindow, MaintenanceWindowDto>(entity);
    }

    public async Task DeleteMaintenanceAsync(Guid id)
    {
        await AuthorizationService.CheckAsync(MonitoringPermissions.Services.Edit);

        await _maintenanceRepository.DeleteAsync(id);
    }

    private IQueryable<MonitoringTarget> ApplySorting(IQueryable<MonitoringTarget> query, string? sorting)
    {
        if (sorting.IsNullOrWhiteSpace())
        {
            return query.OrderBy(target => target.Name);
        }

        var normalized = sorting!.Trim();
        var lower = normalized.ToLowerInvariant();

        return lower switch
        {
            "name desc" or "name descending" => query.OrderByDescending(target => target.Name),
            "name" or "name asc" => query.OrderBy(target => target.Name),
            "lastcheckedat desc" or "lastcheckedat" or "lastcheckedat descending" => query.OrderByDescending(target => target.LastCheckedAt),
            "nextdueat" or "nextdueat asc" => query.OrderBy(target => target.NextDueAt),
            "nextdueat desc" or "nextdueat descending" => query.OrderByDescending(target => target.NextDueAt),
            _ => query.OrderBy(target => target.Name)
        };
    }

    private void ValidateInput(CreateUpdateMonitoringTargetDto input)
    {
        if (input == null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        if (input.Name.IsNullOrWhiteSpace() || input.Name.Length is < 2 or > 128)
        {
            throw new UserFriendlyException("Name must be between 2 and 128 characters.");
        }

        if (input.Endpoint.IsNullOrWhiteSpace())
        {
            throw new UserFriendlyException("Endpoint is required.");
        }

        if (input.CheckIntervalSeconds < 10)
        {
            throw new UserFriendlyException("Check interval must be at least 10 seconds.");
        }

        if (input.TimeoutSeconds <= 0)
        {
            throw new UserFriendlyException("Timeout must be greater than zero.");
        }

        if (input.RetryDelaySeconds < 1)
        {
            throw new UserFriendlyException("Retry delay must be at least 1 second.");
        }

        if (input.MaxRetryAttempts < 0)
        {
            throw new UserFriendlyException("Retry attempts cannot be negative.");
        }

        ValidateEndpointAndSettings(input);
    }

    private void ValidateMaintenanceWindow(CreateUpdateMaintenanceWindowDto input)
    {
        if (input.StartUtc == default)
        {
            throw new UserFriendlyException("StartUtc is required.");
        }

        if (input.EndUtc == default)
        {
            throw new UserFriendlyException("EndUtc is required.");
        }

        if (input.EndUtc <= input.StartUtc)
        {
            throw new UserFriendlyException("EndUtc must be greater than StartUtc.");
        }

        if (!input.IsGlobal && !input.TargetId.HasValue)
        {
            throw new UserFriendlyException("TargetId is required for non-global maintenance windows.");
        }

        if (!input.Reason.IsNullOrWhiteSpace() && input.Reason!.Length > MaintenanceWindowConsts.ReasonMaxLength)
        {
            throw new UserFriendlyException($"Reason cannot exceed {MaintenanceWindowConsts.ReasonMaxLength} characters.");
        }
    }

    private async Task<MonitoringTarget> EnsureTargetExistsAsync(Guid targetId)
    {
        var target = await _repository.FindAsync(targetId);
        if (target == null)
        {
            throw new EntityNotFoundException(typeof(MonitoringTarget), targetId);
        }

        return target;
    }

    private void ValidateEndpointAndSettings(CreateUpdateMonitoringTargetDto input)
    {
        switch (input.Type)
        {
            case ServiceType.Website:
                EnsureEndpointValid(input.Endpoint, EndpointType.Website, "Endpoint must be an absolute HTTP or HTTPS URL.");
                DeserializeSettings<WebsiteSettings>(input.SettingsJson, "Website settings");
                break;
            case ServiceType.Api:
                EnsureEndpointValid(input.Endpoint, EndpointType.Api, "Endpoint must be an absolute HTTP or HTTPS URL.");
                DeserializeSettings<ApiSettings>(input.SettingsJson, "API settings");
                break;
            case ServiceType.Tcp:
                ValidateTcpConfiguration(input.Endpoint, input.SettingsJson);
                break;
            case ServiceType.Redis:
                ValidateRedisConfiguration(input.Endpoint, input.SettingsJson);
                break;
            default:
                throw new UserFriendlyException("Unsupported service type.");
        }
    }

    private void ValidateTcpConfiguration(string endpoint, string? settingsJson)
    {
        if (EndpointParser.TryParse(endpoint, EndpointType.Tcp, out _, out _))
        {
            DeserializeSettings<TcpSettings>(settingsJson, "TCP settings");
            return;
        }

        var settings = DeserializeSettings<TcpSettings>(settingsJson, "TCP settings");
        if (settings.Host.IsNullOrWhiteSpace() || !settings.Port.HasValue || settings.Port.Value is < 1 or > 65535)
        {
            throw new UserFriendlyException("Provide a valid host and port for TCP targets.");
        }
    }

    private void ValidateRedisConfiguration(string endpoint, string? settingsJson)
    {
        var endpointValid = EndpointParser.TryParse(endpoint, EndpointType.Redis, out _, out _);
        var settings = DeserializeSettings<RedisSettings>(settingsJson, "Redis settings");

        if (settings.Endpoints is { Length: > 0 })
        {
            foreach (var candidate in settings.Endpoints)
            {
                if (!EndpointParser.TryParse(candidate, EndpointType.Redis, out _, out _))
                {
                    throw new UserFriendlyException("Redis endpoints must be expressed as host:port values.");
                }
            }
        }

        var mode = settings.Mode?.Trim().ToLowerInvariant() ?? "standalone";
        if (mode == "sentinel")
        {
            if (settings.Sentinels == null || settings.Sentinels.Length == 0)
            {
                throw new UserFriendlyException("Sentinel configuration requires at least one sentinel endpoint.");
            }

            foreach (var sentinel in settings.Sentinels)
            {
                if (!EndpointParser.TryParse(sentinel, EndpointType.Redis, out _, out _))
                {
                    throw new UserFriendlyException("Sentinel endpoints must be valid host:port values.");
                }
            }

            if (settings.SentinelMasterName.IsNullOrWhiteSpace())
            {
                throw new UserFriendlyException("Sentinel master name is required.");
            }
        }

        if (!endpointValid)
        {
            var hasEndpoint = settings.Endpoints != null && settings.Endpoints.Any(e => EndpointParser.TryParse(e, EndpointType.Redis, out _, out _));
            if (!hasEndpoint)
            {
                throw new UserFriendlyException("Provide at least one valid Redis endpoint.");
            }
        }
    }

    private static void EnsureEndpointValid(string endpoint, EndpointType type, string errorMessage)
    {
        if (!EndpointParser.TryParse(endpoint, type, out _, out _))
        {
            throw new UserFriendlyException(errorMessage);
        }
    }

    private T DeserializeSettings<T>(string? json, string context)
        where T : class, new()
    {
        if (json.IsNullOrWhiteSpace())
        {
            return new T();
        }

        try
        {
            var settings = JsonSerializer.Deserialize<T>(json, SettingsSerializerOptions);
            if (settings == null)
            {
                throw new UserFriendlyException($"{context} JSON is invalid.");
            }

            return settings;
        }
        catch (JsonException ex)
        {
            Logger.LogWarning(ex, "Failed to deserialize monitoring settings for {Context}", context);
            throw new UserFriendlyException($"{context} JSON is invalid.");
        }
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
