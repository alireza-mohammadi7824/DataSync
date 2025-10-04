using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monitoring.Alerts;
using Monitoring.HealthChecks;
using Monitoring.Options;
using Monitoring.Targets;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace Monitoring.Execution;

public sealed class MonitoringCheckService : IMonitoringCheckService
{
    private readonly HealthCheckExecutor _executor;
    private readonly IRepository<MonitoringTarget, Guid> _targetRepository;
    private readonly IRepository<ServiceStatusHistory, Guid> _historyRepository;
    private readonly IRepository<OutageWindow, Guid> _outageRepository;
    private readonly IRepository<AlertPolicy, Guid> _alertPolicyRepository;
    private readonly IRepository<MaintenanceWindow, Guid> _maintenanceRepository;
    private readonly INotificationChannelResolver _channelResolver;
    private readonly IClock _clock;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly IGuidGenerator _guidGenerator;
    private readonly MonitoringOptions _options;
    private readonly IOptionsMonitor<MonitoringAlertsOptions> _alertsOptions;
    private readonly ILogger<MonitoringCheckService> _logger;

    public MonitoringCheckService(
        HealthCheckExecutor executor,
        IRepository<MonitoringTarget, Guid> targetRepository,
        IRepository<ServiceStatusHistory, Guid> historyRepository,
        IRepository<OutageWindow, Guid> outageRepository,
        IRepository<AlertPolicy, Guid> alertPolicyRepository,
        IRepository<MaintenanceWindow, Guid> maintenanceRepository,
        INotificationChannelResolver channelResolver,
        IClock clock,
        IUnitOfWorkManager unitOfWorkManager,
        IGuidGenerator guidGenerator,
        IOptions<MonitoringOptions> options,
        IOptionsMonitor<MonitoringAlertsOptions> alertsOptions,
        ILogger<MonitoringCheckService> logger)
    {
        _executor = executor;
        _targetRepository = targetRepository;
        _historyRepository = historyRepository;
        _outageRepository = outageRepository;
        _alertPolicyRepository = alertPolicyRepository;
        _maintenanceRepository = maintenanceRepository;
        _channelResolver = channelResolver;
        _clock = clock;
        _unitOfWorkManager = unitOfWorkManager;
        _guidGenerator = guidGenerator;
        _options = options.Value;
        _alertsOptions = alertsOptions;
        _logger = logger;
    }

    public async Task<CheckProcessingResult> RunAsync(
        MonitoringTarget target,
        string triggerSource,
        bool dispatchAlerts,
        CancellationToken cancellationToken)
    {
        var previousLastUpAt = target.LastUpAt;

        var execution = await _executor.ExecuteAsync(target, triggerSource, cancellationToken);
        if (execution.IsSkipped)
        {
            return CheckProcessingResult.Skipped(execution.SkipReason ?? "Skipped");
        }

        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);

        var outcome = await MonitoringTargetCheckProcessor.ApplyResultAsync(
            target,
            execution.Result,
            execution.CompletedAt,
            _historyRepository,
            _outageRepository,
            _guidGenerator,
            cancellationToken);

        await _targetRepository.UpdateAsync(target, autoSave: true, cancellationToken: cancellationToken);
        await uow.CompleteAsync();

        var alertsDispatched = 0;
        if (dispatchAlerts)
        {
            var dispatches = await EvaluateAlertsAsync(target, outcome, execution.Result, execution.CompletedAt, previousLastUpAt, cancellationToken);
            if (dispatches.Count > 0)
            {
                alertsDispatched = dispatches.Count;
                await DispatchAlertsAsync(dispatches, cancellationToken);
            }
        }

        return CheckProcessingResult.Completed(execution.Result, execution.CompletedAt, alertsDispatched, outcome);
    }

    private async Task<List<AlertDispatch>> EvaluateAlertsAsync(
        MonitoringTarget target,
        MonitoringCheckOutcome outcome,
        HealthCheckResult result,
        DateTime timestamp,
        DateTime? previousLastUpAt,
        CancellationToken cancellationToken)
    {
        var dispatches = new List<AlertDispatch>();

        var policy = await _alertPolicyRepository.FirstOrDefaultAsync(x => x.TargetId == target.Id, cancellationToken: cancellationToken);
        var defaults = _options.AlertDefaults;

        var enabled = policy?.Enabled ?? defaults.Enabled;
        if (!enabled)
        {
            return dispatches;
        }

        var notifyAfterFailures = policy?.NotifyAfterFailures ?? defaults.NotifyAfterFailures;
        var repeatMinutes = policy?.RepeatMinutes ?? ResolveDefaultRepeatMinutes(defaults);
        var recoverQuietMinutes = policy?.RecoverQuietMinutes ?? defaults.RecoverQuietMinutes;
        var suppressDuringMaintenance = policy?.SuppressDuringMaintenance ?? defaults.SuppressDuringMaintenance;

        var channels = ResolveChannels(policy?.ChannelsJson, target.Id);
        if (channels.Count == 0)
        {
            var fallback = _options.AlertDefaults.DefaultChannels ?? new Dictionary<string, string[]>();
            channels = CloneChannels(fallback);
            if (channels.Count == 0)
            {
                _logger.LogDebug(
                    "No alert channels configured for target {TargetId}; alerts will be suppressed",
                    target.Id);
            }
        }

        if (channels.Count == 0)
        {
            return dispatches;
        }

        var underMaintenance = suppressDuringMaintenance && await HasActiveMaintenanceAsync(target.Id, timestamp, cancellationToken);

        var evaluation = AlertEvaluator.Evaluate(new AlertEvaluationInput(
            outcome.PreviousStatus,
            outcome.CurrentStatus,
            target.ConsecutiveFailures,
            timestamp,
            notifyAfterFailures,
            repeatMinutes,
            recoverQuietMinutes,
            enabled,
            underMaintenance,
            outcome.ActiveOutage,
            outcome.ClosedOutage,
            previousLastUpAt));

        if (!evaluation.ShouldAlert)
        {
            return dispatches;
        }

        var payload = new AlertPayload(
            evaluation.EventType,
            timestamp,
            target.Name,
            target.Type.ToString(),
            target.Endpoint,
            result.ErrorSummary,
            result.ResponseTimeMs,
            evaluation.CurrentOutage);

        dispatches.AddRange(AlertDispatch.Create(target.Id, payload, channels));
        return dispatches;
    }

    private int ResolveDefaultRepeatMinutes(MonitoringOptions.AlertDefaultsOptions defaults)
    {
        var cooldownSeconds = _alertsOptions.CurrentValue.DefaultCooldownSeconds;
        if (cooldownSeconds > 0)
        {
            return Math.Max(1, (int)Math.Ceiling(cooldownSeconds / 60d));
        }

        return Math.Max(1, defaults.RepeatMinutes);
    }

    private async Task DispatchAlertsAsync(List<AlertDispatch> dispatches, CancellationToken cancellationToken)
    {
        foreach (var dispatch in dispatches)
        {
            try
            {
                var channel = _channelResolver.Resolve(dispatch.Channel);
                await channel.SendAsync(dispatch.TargetSnapshot, dispatch.Payload, cancellationToken);
                _logger.LogInformation(
                    "Alert sent for target {TargetId} via {Channel} for event {EventType}",
                    dispatch.TargetSnapshot.Id,
                    dispatch.Channel,
                    dispatch.Payload.EventType);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to send alert for target {TargetId} via {Channel}",
                    dispatch.TargetSnapshot.Id,
                    dispatch.Channel);
            }
        }
    }

    private async Task<bool> HasActiveMaintenanceAsync(Guid targetId, DateTime timestamp, CancellationToken cancellationToken)
    {
        return await _maintenanceRepository.AnyAsync(
            window => window.StartUtc <= timestamp && window.EndUtc >= timestamp &&
                      (window.TargetId == null || window.TargetId == targetId),
            cancellationToken);
    }

    private static Dictionary<string, string[]> CloneChannels(Dictionary<string, string[]> source)
    {
        return source.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
    }

    private Dictionary<string, string[]> ResolveChannels(string? json, Guid targetId)
    {
        if (json.IsNullOrWhiteSpace())
        {
            return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string[]>>(json!);
            if (parsed == null)
            {
                return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            }

            return CloneChannels(parsed);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "Failed to parse alert channels configuration for target {TargetId}",
                targetId);
            return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
