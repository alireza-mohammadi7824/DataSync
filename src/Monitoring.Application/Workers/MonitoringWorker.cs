using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Monitoring.HealthChecks;
using Monitoring.Targets;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace Monitoring.Workers;

public class MonitoringWorker : BackgroundService
{
    private const string TriggerSource = "worker";
    private static readonly TimeSpan OverrideInterval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan WorkerInterval = TimeSpan.FromMinutes(1);

    private readonly ILogger<MonitoringWorker> _logger;
    private readonly IRepository<MonitoringTarget, Guid> _targetRepository;
    private readonly IHealthCheckProviderResolver _providerResolver;
    private readonly IClock _clock;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public MonitoringWorker(
        ILogger<MonitoringWorker> logger,
        IRepository<MonitoringTarget, Guid> targetRepository,
        IHealthCheckProviderResolver providerResolver,
        IClock clock,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _logger = logger;
        _targetRepository = targetRepository;
        _providerResolver = providerResolver;
        _clock = clock;
        _unitOfWorkManager = unitOfWorkManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(WorkerInterval);

        while (!stoppingToken.IsCancellationRequested)
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

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
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

        await using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);

        var recordedAt = _clock.Now;
        MonitoringTargetCheckProcessor.ApplyResult(target, result, recordedAt);

        await _targetRepository.UpdateAsync(target, autoSave: true, cancellationToken: cancellationToken);
        await uow.CompleteAsync();
    }
}
