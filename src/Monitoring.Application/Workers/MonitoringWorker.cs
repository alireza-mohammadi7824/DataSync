using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monitoring.HealthChecks;
using Monitoring.Targets;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;
using Volo.Abp.Uow;
using Volo.Abp.Threading;

namespace Monitoring.Workers;

public class MonitoringWorker : AsyncPeriodicBackgroundWorkerBase, ISingletonDependency
{
    private const string TriggerSource = "worker";
    private static readonly TimeSpan OverrideInterval = TimeSpan.FromMinutes(30);

    private readonly IRepository<MonitoringTarget, Guid> _targetRepository;
    private readonly IHealthCheckProviderResolver _providerResolver;
    private readonly IClock _clock;

    public MonitoringWorker(
        AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory,
        IRepository<MonitoringTarget, Guid> targetRepository,
        IHealthCheckProviderResolver providerResolver,
        IClock clock)
        : base(timer, serviceScopeFactory)
    {
        _targetRepository = targetRepository;
        _providerResolver = providerResolver;
        _clock = clock;

        Timer.Period = (int)TimeSpan.FromMinutes(1).TotalMilliseconds;
        Timer.RunOnStart = true;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var cancellationToken = workerContext.CancellationToken;

        var uowOptions = new AbpUnitOfWorkOptions
        {
            IsTransactional = false
        };

        using var uow = UnitOfWorkManager.Begin(uowOptions, requiresNew: true);

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

        await uow.CompleteAsync();
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
            Logger.LogWarning(ex, "Monitoring worker failed for target {TargetId}", target.Id);
            result = new HealthCheckResult(false, null, "Worker error", TriggerSource);
        }

        var recordedAt = _clock.Now;

        MonitoringTargetCheckProcessor.ApplyResult(target, result, recordedAt);

        await _targetRepository.UpdateAsync(target, autoSave: true, cancellationToken: cancellationToken);
    }
}
