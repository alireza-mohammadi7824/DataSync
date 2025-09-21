using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monitoring.HealthChecks;
using Monitoring.Options;
using Monitoring.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace Monitoring.Targets;

public class MonitoringTargetAppService : ApplicationService, IMonitoringTargetAppService
{
    private const int DefaultPageSize = 12;
    private const int MaxPageSize = 100;

    private static readonly JsonSerializerOptions SettingsSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions AlertChannelsSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IRepository<MonitoringTarget, Guid> _repository;
    private readonly IHealthCheckProviderResolver _providerResolver;
    private readonly IRepository<ServiceStatusHistory, Guid> _historyRepository;
    private readonly IRepository<OutageWindow, Guid> _outageRepository;
    private readonly IRepository<AlertPolicy, Guid> _alertPolicyRepository;
    private readonly IRepository<MaintenanceWindow, Guid> _maintenanceRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly MonitoringOptions _options;

    public MonitoringTargetAppService(
        IRepository<MonitoringTarget, Guid> repository,
        IHealthCheckProviderResolver providerResolver,
        IRepository<ServiceStatusHistory, Guid> historyRepository,
        IRepository<OutageWindow, Guid> outageRepository,
        IRepository<AlertPolicy, Guid> alertPolicyRepository,
        IRepository<MaintenanceWindow, Guid> maintenanceRepository,
        IGuidGenerator guidGenerator,
        IOptions<MonitoringOptions> options)
    {
        _repository = repository;
        _providerResolver = providerResolver;
        _historyRepository = historyRepository;
        _outageRepository = outageRepository;
        _alertPolicyRepository = alertPolicyRepository;
        _maintenanceRepository = maintenanceRepository;
        _guidGenerator = guidGenerator;
        _options = options.Value;
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

        var entity = await _repository.GetAsync(id);

        var now = Clock.Now;
        entity.SetLastCheckedAt(now);
        entity.SetNextDueAt(now);
        entity.SetCurrentStatus(ServiceStatus.Checking);
        entity.SetLastStatusChangeAt(now);
        entity.SetConsecutiveFailures(0);

        await _repository.UpdateAsync(entity, autoSave: true);
    }

    public async Task<HealthCheckResultDto> CheckNowAsync(Guid id)
    {
        await AuthorizationService.CheckAsync(MonitoringPermissions.Services.Run);

        var target = await _repository.GetAsync(id);

        var execution = await ExecuteCheckAsync(target, "manual");
        var result = execution.Result;
        var timestamp = execution.Timestamp;

        await _repository.UpdateAsync(target, autoSave: true);

        return CreateResultDto(target, result, timestamp);
    }

    public async Task<List<HealthCheckResultDto>> CheckAllAsync()
    {
        await AuthorizationService.CheckAsync(MonitoringPermissions.Services.Run);

        var targets = await _repository.GetListAsync(target => target.IsActive);

        var results = new List<HealthCheckResultDto>(targets.Count);

        foreach (var target in targets)
        {
            var execution = await ExecuteCheckAsync(target, "api");
            await _repository.UpdateAsync(target, autoSave: true);

            results.Add(CreateResultDto(target, execution.Result, execution.Timestamp));
        }

        return results;
    }

    public async Task<int> CheckAllNowAsync()
    {
        await AuthorizationService.CheckAsync(MonitoringPermissions.Services.Run);

        var targets = await _repository.GetListAsync(target => target.IsActive);
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

            await _repository.UpdateAsync(target, autoSave: true);
        }

        return targets.Count;
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

    public async Task<AlertPolicyDto> GetAlertPolicyAsync(Guid targetId)
    {
        await AuthorizationService.CheckAsync(MonitoringPermissions.Services.View);

        await EnsureTargetExistsAsync(targetId);

        var policy = await _alertPolicyRepository.FirstOrDefaultAsync(x => x.TargetId == targetId);
        if (policy == null)
        {
            var defaults = _options.AlertDefaults;
            return new AlertPolicyDto
            {
                TargetId = targetId,
                Enabled = defaults.Enabled,
                NotifyAfterFailures = defaults.NotifyAfterFailures,
                RepeatMinutes = defaults.RepeatMinutes,
                RecoverQuietMinutes = defaults.RecoverQuietMinutes,
                ChannelsJson = SerializeDefaultChannels(defaults.DefaultChannels),
                SuppressDuringMaintenance = defaults.SuppressDuringMaintenance,
                IsInherited = true
            };
        }

        var dto = ObjectMapper.Map<AlertPolicy, AlertPolicyDto>(policy);
        dto.IsInherited = false;
        return dto;
    }

    public async Task<AlertPolicyDto> UpsertAlertPolicyAsync(Guid targetId, AlertPolicyDto input)
    {
        await AuthorizationService.CheckAsync(MonitoringPermissions.Services.Edit);

        if (input == null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        await EnsureTargetExistsAsync(targetId);

        if (input.TargetId != Guid.Empty && input.TargetId != targetId)
        {
            throw new UserFriendlyException("TargetId mismatch.");
        }

        input.TargetId = targetId;

        ValidateAlertPolicyInput(input);

        var normalizedChannels = NormalizeChannelsJson(input.ChannelsJson);

        var existing = await _alertPolicyRepository.FirstOrDefaultAsync(x => x.TargetId == targetId);
        if (existing == null)
        {
            existing = new AlertPolicy(
                _guidGenerator.Create(),
                targetId,
                input.Enabled,
                input.NotifyAfterFailures,
                input.RepeatMinutes,
                input.RecoverQuietMinutes,
                normalizedChannels,
                input.SuppressDuringMaintenance);

            await _alertPolicyRepository.InsertAsync(existing, autoSave: true);
        }
        else
        {
            existing.Update(
                input.Enabled,
                input.NotifyAfterFailures,
                input.RepeatMinutes,
                input.RecoverQuietMinutes,
                normalizedChannels,
                input.SuppressDuringMaintenance);

            await _alertPolicyRepository.UpdateAsync(existing, autoSave: true);
        }

        var dto = ObjectMapper.Map<AlertPolicy, AlertPolicyDto>(existing);
        dto.TargetId = targetId;
        dto.IsInherited = false;
        return dto;
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

        if (input.TargetId.HasValue)
        {
            await EnsureTargetExistsAsync(input.TargetId.Value);
        }

        var entity = new MaintenanceWindow(
            _guidGenerator.Create(),
            input.TargetId,
            input.StartUtc,
            input.EndUtc,
            input.Reason);

        var created = await _maintenanceRepository.InsertAsync(entity, autoSave: true);
        return ObjectMapper.Map<MaintenanceWindow, MaintenanceWindowDto>(created);
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

    private void ValidateAlertPolicyInput(AlertPolicyDto input)
    {
        if (input.NotifyAfterFailures < 1)
        {
            throw new UserFriendlyException("NotifyAfterFailures must be at least 1.");
        }

        if (input.RepeatMinutes < 1)
        {
            throw new UserFriendlyException("RepeatMinutes must be at least 1.");
        }

        if (input.RecoverQuietMinutes < 0)
        {
            throw new UserFriendlyException("RecoverQuietMinutes cannot be negative.");
        }

        if (!input.ChannelsJson.IsNullOrWhiteSpace())
        {
            if (input.ChannelsJson!.Length > AlertPolicyConsts.ChannelsJsonMaxLength)
            {
                throw new UserFriendlyException($"ChannelsJson cannot exceed {AlertPolicyConsts.ChannelsJsonMaxLength} characters.");
            }

            try
            {
                JsonDocument.Parse(input.ChannelsJson);
            }
            catch (JsonException)
            {
                throw new UserFriendlyException("ChannelsJson must be valid JSON.");
            }
        }
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

        if (!input.Reason.IsNullOrWhiteSpace() && input.Reason!.Length > MaintenanceWindowConsts.ReasonMaxLength)
        {
            throw new UserFriendlyException($"Reason cannot exceed {MaintenanceWindowConsts.ReasonMaxLength} characters.");
        }
    }

    private string? NormalizeChannelsJson(string? channelsJson)
    {
        if (channelsJson.IsNullOrWhiteSpace())
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(channelsJson);
            var normalized = JsonSerializer.Serialize(document.RootElement, AlertChannelsSerializerOptions);

            if (normalized.Length > AlertPolicyConsts.ChannelsJsonMaxLength)
            {
                throw new UserFriendlyException($"ChannelsJson cannot exceed {AlertPolicyConsts.ChannelsJsonMaxLength} characters.");
            }

            return normalized;
        }
        catch (JsonException)
        {
            throw new UserFriendlyException("ChannelsJson must be valid JSON.");
        }
    }

    private string? SerializeDefaultChannels(Dictionary<string, string[]>? channels)
    {
        if (channels == null || channels.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(channels, AlertChannelsSerializerOptions);
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
                EnsureValidHttpEndpoint(input.Endpoint);
                DeserializeSettings<WebsiteSettings>(input.SettingsJson, "Website settings");
                break;
            case ServiceType.Api:
                EnsureValidHttpEndpoint(input.Endpoint);
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
        if (EndpointParser.TryParseHostPort(endpoint, out _, out _))
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
        var endpointValid = EndpointParser.TryParseHostPort(endpoint, out _, out _);
        var settings = DeserializeSettings<RedisSettings>(settingsJson, "Redis settings");

        if (settings.Endpoints is { Length: > 0 })
        {
            foreach (var candidate in settings.Endpoints)
            {
                if (!EndpointParser.TryParseHostPort(candidate, out _, out _))
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
                if (!EndpointParser.TryParseHostPort(sentinel, out _, out _))
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
            var hasEndpoint = settings.Endpoints != null && settings.Endpoints.Any(e => EndpointParser.TryParseHostPort(e, out _, out _));
            if (!hasEndpoint)
            {
                throw new UserFriendlyException("Provide at least one valid Redis endpoint.");
            }
        }
    }

    private static void EnsureValidHttpEndpoint(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new UserFriendlyException("Endpoint must be an absolute HTTP or HTTPS URL.");
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
        _ = await MonitoringTargetCheckProcessor.ApplyResultAsync(
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
