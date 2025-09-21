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
using Volo.Abp.Data;
using Volo.Abp.Threading;

namespace Monitoring.Workers;

public class MonitoringWorker : AsyncPeriodicBackgroundWorkerBase, ISingletonDependency
{
    private const string TriggerSource = "worker";
    private const string LastResponseTimePropertyName = "Monitoring:LastResponseTimeMs";
    private const string LastErrorSummaryPropertyName = "Monitoring:LastErrorSummary";
    private const string LastTriggerSourcePropertyName = "Monitoring:LastTriggerSource";
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

            await HandleTargetAsync(target, now, cancellationToken);
        }

        await uow.CompleteAsync();
    }

    private async Task HandleTargetAsync(MonitoringTarget target, DateTime now, CancellationToken cancellationToken)
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

        var previousStatus = target.CurrentStatus;

        target.SetLastCheckedAt(now);
        target.SetProperty(LastResponseTimePropertyName, result.ResponseTimeMs);
        target.SetProperty(LastErrorSummaryPropertyName, result.ErrorSummary);
        target.SetProperty(LastTriggerSourcePropertyName, result.TriggerSource);

        ServiceStatus newStatus;

        if (result.IsSuccess)
        {
            newStatus = ServiceStatus.Online;
            target.SetConsecutiveFailures(0);
            target.SetLastUpAt(now);
            target.SetNextDueAt(now.AddSeconds(target.CheckIntervalSeconds));
            target.SetFirstDownAt(null);
        }
        else
        {
            var failures = target.ConsecutiveFailures + 1;
            target.SetConsecutiveFailures(failures);

            if (failures < target.MaxRetryAttempts)
            {
                newStatus = ServiceStatus.Checking;
                var retryDelaySeconds = target.RetryDelaySeconds > 0 ? target.RetryDelaySeconds : 1;
                target.SetNextDueAt(now.AddSeconds(retryDelaySeconds));
            }
            else
            {
                newStatus = ServiceStatus.Offline;
                target.SetNextDueAt(now.AddSeconds(target.CheckIntervalSeconds));

                if (!target.FirstDownAt.HasValue)
                {
                    target.SetFirstDownAt(now);
                }
            }
        }

        target.SetCurrentStatus(newStatus);

        if (newStatus != previousStatus)
        {
            target.SetLastStatusChangeAt(now);
        }

        await _targetRepository.UpdateAsync(target, autoSave: true, cancellationToken: cancellationToken);
    }
}
