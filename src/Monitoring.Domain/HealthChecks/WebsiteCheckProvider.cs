using System.Threading;
using System.Threading.Tasks;
using Monitoring.Targets;

namespace Monitoring.HealthChecks;

public class WebsiteCheckProvider : IHealthCheckProvider
{
    public Task<HealthCheckResult> CheckAsync(MonitoringTarget target, CancellationToken cancellationToken = default)
    {
        // TODO: Implement website health check logic
        return Task.FromResult(new HealthCheckResult(false, null, null, nameof(WebsiteCheckProvider)));
    }
}
