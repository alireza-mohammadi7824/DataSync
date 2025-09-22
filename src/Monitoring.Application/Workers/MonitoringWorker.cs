using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Monitoring.Execution;
using Monitoring.Targets;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;

namespace Monitoring.Workers;

public class MonitoringWorker : BackgroundService
{
    private const string TriggerSource = "worker";
    private static readonly TimeSpan OverrideInterval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan WorkerInterval = TimeSpan.FromMinutes(1);

    private readonly ILogger<MonitoringWorker> _logger;
    private readonly IRepository<MonitoringTarget, Guid> _targetRepository;
    private readonly IMonitoringCheckService _checkService;
    private readonly IClock _clock;

    public MonitoringWorker(
        ILogger<MonitoringWorker> logger,
        IRepository<MonitoringTarget, Guid> targetRepository,
        IMonitoringCheckService checkService,
        IClock clock)
    {
        _logger = logger;
        _targetRepository = targetRepository;
        _checkService = checkService;
        _clock = clock;
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

            var result = await _checkService.RunAsync(target, TriggerSource, true, cancellationToken);
            if (result.IsSkipped)
            {
                _logger.LogDebug(
                    "Monitoring worker skipped target {TargetId}: {Reason}",
                    target.Id,
                    result.SkipReason);
            }
        }
    }
}
