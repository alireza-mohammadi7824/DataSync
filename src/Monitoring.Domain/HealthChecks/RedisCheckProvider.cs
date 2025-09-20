using System.Threading;
using System.Threading.Tasks;
using Monitoring.Targets;

namespace Monitoring.HealthChecks;

public class RedisCheckProvider : IHealthCheckProvider
{
    public Task<HealthCheckResult> CheckAsync(MonitoringTarget target, CancellationToken cancellationToken = default)
    {
        // TODO: Implement Redis health check logic
        return Task.FromResult(new HealthCheckResult(false, null, null, nameof(RedisCheckProvider)));
    }
}
