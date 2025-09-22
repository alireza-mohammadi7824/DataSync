using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Monitoring.Alerts;
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
    private readonly IRepository<MaintenanceWindow, Guid> _maintenanceRepository;
    private readonly AlertEvaluator _alertEvaluator;
    private readonly AlertDispatcher _alertDispatcher;
    private readonly IClock _clock;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ILogger<MonitoringCheckService> _logger;

    public MonitoringCheckService(
        HealthCheckExecutor executor,
        IRepository<MonitoringTarget, Guid> targetRepository,
        IRepository<ServiceStatusHistory, Guid> historyRepository,
        IRepository<OutageWindow, Guid> outageRepository,
        IRepository<MaintenanceWindow, Guid> maintenanceRepository,
        AlertEvaluator alertEvaluator,
        AlertDispatcher alertDispatcher,
        IClock clock,
        IUnitOfWorkManager unitOfWorkManager,
        IGuidGenerator guidGenerator,
        ILogger<MonitoringCheckService> logger)
    {
        _executor = executor;
        _targetRepository = targetRepository;
        _historyRepository = historyRepository;
        _outageRepository = outageRepository;
        _maintenanceRepository = maintenanceRepository;
        _alertEvaluator = alertEvaluator;
        _alertDispatcher = alertDispatcher;
        _clock = clock;
        _unitOfWorkManager = unitOfWorkManager;
        _guidGenerator = guidGenerator;
        _logger = logger;
    }

    public async Task<CheckProcessingResult> RunAsync(
        MonitoringTarget target,
        string triggerSource,
        bool dispatchAlerts,
        CancellationToken cancellationToken)
    {
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
            alertsDispatched = await ProcessAlertsAsync(target, outcome, executionResult, completedAt, cancellationToken);
        }

        return CheckProcessingResult.Completed(executionResult, completedAt, alertsDispatched, outcome);
    }

    private Task<MaintenanceState> GetMaintenanceStateAsync(Guid targetId, DateTime timestamp, CancellationToken cancellationToken)
        => MaintenanceWindowHelper.GetStateAsync(_maintenanceRepository, targetId, timestamp, cancellationToken);

    private async Task<int> ProcessAlertsAsync(
        MonitoringTarget target,
        MonitoringCheckOutcome outcome,
        HealthCheckResult result,
        DateTime completedAt,
        CancellationToken cancellationToken)
    {
        if (outcome.PreviousStatus == outcome.CurrentStatus)
        {
            return 0;
        }

        var maintenanceState = await GetMaintenanceStateAsync(target.Id, completedAt, cancellationToken);
        if (maintenanceState.ShouldSkip || maintenanceState.RecordButDontAlert)
        {
            _logger.LogInformation(
                "Alert suppressed for target {TargetId} due to active maintenance window.",
                target.Id);
            return 0;
        }

        var outage = outcome.CurrentStatus == ServiceStatus.Offline
            ? outcome.ActiveOutage
            : outcome.ClosedOutage;

        var history = new ServiceStatusHistory(
            Guid.Empty,
            target.Id,
            outcome.PreviousStatus,
            outcome.CurrentStatus,
            completedAt,
            result.TriggerSource,
            result.ResponseTimeMs,
            result.ErrorSummary);

        var evaluation = await _alertEvaluator.EvaluateTransitionAsync(
            target,
            history,
            outage,
            completedAt,
            cancellationToken);

        if (!evaluation.ShouldAlert || string.IsNullOrWhiteSpace(evaluation.EventType) || evaluation.Policies.Count == 0)
        {
            return 0;
        }

        var startedAt = outage?.StartedAt ?? completedAt;
        var endedAt = outage?.EndedAt;
        if (outcome.CurrentStatus == ServiceStatus.Online && endedAt == null)
        {
            endedAt = completedAt;
        }

        TimeSpan? duration = null;
        if (outage != null)
        {
            var end = endedAt ?? completedAt;
            var span = end - outage.StartedAt;
            if (span < TimeSpan.Zero)
            {
                span = TimeSpan.Zero;
            }

            duration = span;
        }

        var payload = new AlertPayload(
            target.Id,
            target.Name,
            outcome.CurrentStatus.ToString(),
            startedAt,
            endedAt,
            duration,
            evaluation.EventType!,
            evaluation.Summary);

        return await _alertDispatcher.DispatchAsync(target, evaluation, payload, cancellationToken);
    }
}
