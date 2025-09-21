using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monitoring.Alerts;
using Monitoring.HealthChecks;
using Monitoring.Options;
using Monitoring.Targets;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Timing;
using Volo.Abp.Uow;
using Volo.Abp;

namespace Monitoring.Workers;

public class MonitoringWorker : BackgroundService
{
    private const string TriggerSource = "worker";
    private static readonly TimeSpan OverrideInterval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan WorkerInterval = TimeSpan.FromMinutes(1);

    private readonly ILogger<MonitoringWorker> _logger;
    private readonly IRepository<MonitoringTarget, Guid> _targetRepository;
    private readonly IRepository<ServiceStatusHistory, Guid> _historyRepository;
    private readonly IRepository<OutageWindow, Guid> _outageRepository;
    private readonly IRepository<AlertPolicy, Guid> _alertPolicyRepository;
    private readonly IRepository<MaintenanceWindow, Guid> _maintenanceRepository;
    private readonly IHealthCheckProviderResolver _providerResolver;
    private readonly INotificationChannelResolver _channelResolver;
    private readonly IClock _clock;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly IGuidGenerator _guidGenerator;
    private readonly MonitoringOptions _options;

    public MonitoringWorker(
        ILogger<MonitoringWorker> logger,
        IRepository<MonitoringTarget, Guid> targetRepository,
        IRepository<ServiceStatusHistory, Guid> historyRepository,
        IRepository<OutageWindow, Guid> outageRepository,
        IRepository<AlertPolicy, Guid> alertPolicyRepository,
        IRepository<MaintenanceWindow, Guid> maintenanceRepository,
        IHealthCheckProviderResolver providerResolver,
        INotificationChannelResolver channelResolver,
        IClock clock,
        IUnitOfWorkManager unitOfWorkManager,
        IGuidGenerator guidGenerator,
        IOptions<MonitoringOptions> options)
    {
        _logger = logger;
        _targetRepository = targetRepository;
        _historyRepository = historyRepository;
        _outageRepository = outageRepository;
        _alertPolicyRepository = alertPolicyRepository;
        _maintenanceRepository = maintenanceRepository;
        _providerResolver = providerResolver;
        _channelResolver = channelResolver;
        _clock = clock;
        _unitOfWorkManager = unitOfWorkManager;
        _guidGenerator = guidGenerator;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(WorkerInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await DoCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Monitoring worker cycle failed");
            }
        }
    }

    private async Task DoCycleAsync(CancellationToken cancellationToken)
    {
        var now = _clock.Now;
        var overrideThreshold = now - OverrideInterval;

        var targets = await _targetRepository.GetListAsync(
            target => target.IsActive &&
                      (target.NextDueAt <= now ||
                       !target.LastCheckedAt.HasValue ||
                       target.LastCheckedAt <= overrideThreshold),
            cancellationToken: cancellationToken);

        foreach (var target in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await HandleTargetAsync(target, cancellationToken);
        }
    }

    private async Task HandleTargetAsync(MonitoringTarget target, CancellationToken cancellationToken)
    {
        HealthCheckResult result;
        var previousStatus = target.CurrentStatus;
        var previousLastUpAt = target.LastUpAt;
        try
        {
            var provider = _providerResolver.Resolve(target.Type);
            result = await provider.CheckAsync(target, TriggerSource, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Monitoring worker failed for target {TargetId}", target.Id);
            result = new HealthCheckResult(false, null, "Worker error", TriggerSource);
        }

        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);

        var recordedAt = _clock.Now;
        var outcome = await MonitoringTargetCheckProcessor.ApplyResultAsync(
            target,
            result,
            recordedAt,
            _historyRepository,
            _outageRepository,
            _guidGenerator,
            cancellationToken);

        var alertDispatches = await EvaluateAlertsAsync(
            target,
            outcome,
            result,
            recordedAt,
            previousLastUpAt,
            cancellationToken);

        await _targetRepository.UpdateAsync(target, autoSave: true, cancellationToken: cancellationToken);
        await uow.CompleteAsync();

        if (alertDispatches.Count > 0)
        {
            await DispatchAlertsAsync(alertDispatches, cancellationToken);
        }
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
        var notifyAfterFailures = policy?.NotifyAfterFailures ?? defaults.NotifyAfterFailures;
        var repeatMinutes = policy?.RepeatMinutes ?? defaults.RepeatMinutes;
        var recoverQuietMinutes = policy?.RecoverQuietMinutes ?? defaults.RecoverQuietMinutes;
        var suppressDuringMaintenance = policy?.SuppressDuringMaintenance ?? defaults.SuppressDuringMaintenance;

        var channels = ResolveChannels(policy?.ChannelsJson);
        if (channels.Count == 0)
        {
            var fallback = _options.AlertDefaults.DefaultChannels ?? new Dictionary<string, string[]>();
            channels = CloneChannels(fallback);
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

        if (evaluation.EventType == null)
        {
            return dispatches;
        }

        OutageWindow? outageForAlert = evaluation.Outage;
        if (evaluation.ShouldRecordAlert && outageForAlert != null)
        {
            outageForAlert.RecordAlert(timestamp);
            await _outageRepository.UpdateAsync(outageForAlert, autoSave: true, cancellationToken);
        }

        var configuration = new AlertChannelConfiguration(channels);
        var channelDescriptors = _channelResolver.ResolveChannels(configuration);
        if (channelDescriptors.Count == 0)
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

        var payload = new AlertPayload(
            evaluation.EventType.Value,
            timestamp,
            result.ErrorSummary,
            result.ResponseTimeMs,
            CreateOutageSnapshot(outageForAlert));

        dispatches.Add(new AlertDispatch(snapshot, payload, channelDescriptors));

        return dispatches;
    }

    private async Task<bool> HasActiveMaintenanceAsync(Guid targetId, DateTime timestamp, CancellationToken cancellationToken)
    {
        return await _maintenanceRepository.AnyAsync(
            window => window.StartUtc <= timestamp && window.EndUtc >= timestamp &&
                      (window.TargetId == null || window.TargetId == targetId),
            cancellationToken: cancellationToken);
    }

    private Dictionary<string, string[]> ResolveChannels(string? channelsJson)
    {
        var parsed = NotificationChannelResolver.ParseChannelsJson(channelsJson);
        if (parsed.Count > 0)
        {
            return parsed;
        }

        return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string[]> CloneChannels(Dictionary<string, string[]>? source)
    {
        if (source == null || source.Count == 0)
        {
            return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        }

        var clone = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in source)
        {
            if (kvp.Key.IsNullOrWhiteSpace())
            {
                continue;
            }

            var values = kvp.Value ?? Array.Empty<string>();
            clone[kvp.Key] = values.Length == 0 ? Array.Empty<string>() : values.ToArray();
        }

        return clone;
    }

    private static OutageSnapshot? CreateOutageSnapshot(OutageWindow? outage)
    {
        if (outage == null)
        {
            return null;
        }

        return new OutageSnapshot(
            outage.Id,
            outage.StartedAt,
            outage.EndedAt,
            outage.FailureCount,
            outage.TotalDurationSec);
    }

    private async Task DispatchAlertsAsync(IReadOnlyList<AlertDispatch> dispatches, CancellationToken cancellationToken)
    {
        var sent = 0;
        var failed = 0;

        foreach (var dispatch in dispatches)
        {
            foreach (var descriptor in dispatch.Channels)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await descriptor.Channel.SendAsync(dispatch.Target, dispatch.Payload, cancellationToken);
                    sent++;
                    _logger.LogInformation(
                        "Alert dispatched for target {TargetId} via {Channel} ({EventType}).",
                        dispatch.Target.TargetId,
                        descriptor.Name,
                        dispatch.Payload.EventType);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.LogWarning(
                        ex,
                        "Alert dispatch failed for target {TargetId} via {Channel} ({EventType}).",
                        dispatch.Target.TargetId,
                        descriptor.Name,
                        dispatch.Payload.EventType);
                }
            }
        }

        if (sent > 0 || failed > 0)
        {
            _logger.LogDebug("Alert dispatch summary: {Sent} succeeded, {Failed} failed.", sent, failed);
        }
    }
}
