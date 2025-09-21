using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Monitoring.HealthChecks;
using Monitoring.Targets;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
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
    private readonly IRepository<ServiceStatusHistory, Guid> _historyRepository;
    private readonly IRepository<OutageWindow, Guid> _outageRepository;
    private readonly IHealthCheckProviderResolver _providerResolver;
    private readonly IClock _clock;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly IGuidGenerator _guidGenerator;

    public MonitoringWorker(
        ILogger<MonitoringWorker> logger,
        IRepository<MonitoringTarget, Guid> targetRepository,
        IRepository<ServiceStatusHistory, Guid> historyRepository,
        IRepository<OutageWindow, Guid> outageRepository,
        IHealthCheckProviderResolver providerResolver,
        IClock clock,
        IUnitOfWorkManager unitOfWorkManager,
        IGuidGenerator guidGenerator)
    {
        _logger = logger;
        _targetRepository = targetRepository;
        _historyRepository = historyRepository;
        _outageRepository = outageRepository;
        _providerResolver = providerResolver;
        _clock = clock;
        _unitOfWorkManager = unitOfWorkManager;
        _guidGenerator = guidGenerator;
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
        await MonitoringTargetCheckProcessor.ApplyResultAsync(
            target,
            result,
            recordedAt,
            _historyRepository,
            _outageRepository,
            _guidGenerator,
            cancellationToken);

        await _targetRepository.UpdateAsync(target, autoSave: true, cancellationToken: cancellationToken);
        await uow.CompleteAsync();
    }
}
