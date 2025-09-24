using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Monitoring.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Timing;

namespace Monitoring.Observability;

[Authorize(MonitoringPermissions.Metrics.View)]
public sealed class MonitoringMetricsAppService : ApplicationService, IMonitoringMetricsAppService
{
    private readonly MonitoringMetrics _metrics;

    public MonitoringMetricsAppService(MonitoringMetrics metrics)
    {
        _metrics = metrics;
    }

    public Task<object> GetAsync(CancellationToken cancellationToken = default)
    {
        var (started, succeeded, failed, skipped, locksContended) = _metrics.Snapshot();
        var generatedAtUtc = Clock.Now.ToUniversalTime();

        return Task.FromResult<object>(new
        {
            generatedAtUtc,
            checksStarted = started,
            checksSucceeded = succeeded,
            checksFailed = failed,
            checksSkipped = skipped,
            locksContended
        });
    }
}
