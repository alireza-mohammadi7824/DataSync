using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Monitoring.Execution;
using Volo.Abp.Timing;

namespace Monitoring.Workers;

public sealed class PurgeWorker : BackgroundService
{
    private static readonly TimeSpan TargetRunTime = new(2, 30, 0);

    private readonly MonitoringRetentionManager _retentionManager;
    private readonly IClock _clock;
    private readonly ILogger<PurgeWorker> _logger;

    public PurgeWorker(
        MonitoringRetentionManager retentionManager,
        IClock clock,
        ILogger<PurgeWorker> logger)
    {
        _retentionManager = retentionManager;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = _clock.Now;
            var delay = GetDelay(now);

            if (delay > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }

            try
            {
                await _retentionManager.PurgeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Monitoring retention purge failed");
            }
        }
    }

    private static TimeSpan GetDelay(DateTime now)
    {
        var todayRun = new DateTime(now.Year, now.Month, now.Day, TargetRunTime.Hours, TargetRunTime.Minutes, TargetRunTime.Seconds, now.Kind);
        if (now <= todayRun)
        {
            return todayRun - now;
        }

        var nextRun = todayRun.AddDays(1);
        return nextRun - now;
    }
}
