using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monitoring.Alerts;
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
        _logger = logger;
    }

    public async Task<CheckProcessingResult> RunAsync(
        MonitoringTarget target,
        string triggerSource,
        bool dispatchAlerts,
        CancellationToken cancellationToken)
    {
        var previousLastUpAt = target.LastUpAt;

        var executionResult = await _executor.ExecuteAsync(target, triggerSource, cancellationToken);
        if (executionResult.IsSkipped)
        {
            return CheckProcessingResult.Skipped(executionResult.SkipReason ?? "already-checking");
        }

        var completedAt = executionResult.CompletedAt == default ? _clock.Now : executionResult.CompletedAt;

        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);

        var outcome = await MonitoringTargetCheckProcessor.ApplyResultAsync(
            target,
            executionResult,
            completedAt,
            _historyRepository,
            _outageRepository,
            _guidGenerator,
            executionResult.SuppressOutageProcessing,
            cancellationToken);

        await _targetRepository.UpdateAsync(target, autoSave: true, cancellationToken: cancellationToken);
        await uow.CompleteAsync();

        var alertsDispatched = 0;
        if (dispatchAlerts && !executionResult.SuppressAlerts)
        {
            var dispatches = await EvaluateAlertsAsync(target, outcome, executionResult, completedAt, previousLastUpAt, cancellationToken);
            if (dispatches.Count > 0)
            {
                alertsDispatched = dispatches.Count;
                await DispatchAlertsAsync(dispatches, cancellationToken);
            }
        }

        return CheckProcessingResult.Completed(executionResult, completedAt, alertsDispatched, outcome);
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
        var repeatMinutes = policy?.RepeatMinutes ?? defaults.RepeatMinutes;
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

        var configuration = new AlertChannelConfiguration(channels);
        var channelDescriptors = _channelResolver.ResolveChannels(configuration);
        if (channelDescriptors.Count == 0)
        {
            return dispatches;
        }

        var maintenanceState = await GetMaintenanceStateAsync(target.Id, timestamp, cancellationToken);
        var underMaintenance = suppressDuringMaintenance && maintenanceState.HasActive;

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

        var snapshot = new TargetSnapshot(
            target.Id,
            target.Name,
            target.Type,
            target.Endpoint,
            target.CurrentStatus,
            target.LastCheckedAt,
            target.LastStatusChangeAt,
            target.FirstDownAt,
            target.LastUpAt,
            target.Category);

        var eventType = evaluation.EventType ?? AlertEventType.Down;
        var payload = new AlertPayload(
            eventType,
            timestamp,
            result.ErrorSummary,
            result.ResponseTimeMs,
            evaluation.CurrentOutage);

        var snapshotSummary = $"{snapshot.Name} ({snapshot.Status})";
        var payloadSummary = payload.Summary;

        foreach (var descriptor in channelDescriptors)
        {
            if (descriptor.Channel == null)
            {
                continue;
            }

            var dispatch = AlertDispatch.Create(
                descriptor.Name,
                snapshotSummary,
                payloadSummary,
                snapshot,
                payload,
                descriptor.Channel);

            dispatches.Add(dispatch);
        }
        return dispatches;
    }

    private async Task DispatchAlertsAsync(List<AlertDispatch> dispatches, CancellationToken cancellationToken)
    {
        foreach (var dispatch in dispatches)
        {
            try
            {
                var channel = dispatch.ChannelInstance ?? _channelResolver.Resolve(dispatch.Channel);
                if (channel == null || dispatch.SnapshotModel == null || dispatch.PayloadModel == null)
                {
                    continue;
                }

                await channel.SendAsync(dispatch.SnapshotModel, dispatch.PayloadModel, cancellationToken);
                _logger.LogInformation(
                    "Alert sent for target {TargetId} via {Channel} for event {EventType}",
                    dispatch.SnapshotModel.TargetId,
                    dispatch.Channel,
                    dispatch.PayloadModel.EventType);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to send alert for target {TargetId} via {Channel}",
                    dispatch.SnapshotModel?.TargetId,
                    dispatch.Channel);
            }
        }
    }

    private Task<MaintenanceState> GetMaintenanceStateAsync(Guid targetId, DateTime timestamp, CancellationToken cancellationToken)
        => MaintenanceWindowHelper.GetStateAsync(_maintenanceRepository, targetId, timestamp, cancellationToken);

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
