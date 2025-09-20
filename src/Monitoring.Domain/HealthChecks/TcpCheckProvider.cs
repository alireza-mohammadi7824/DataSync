using System.Threading;
using System.Threading.Tasks;
using Monitoring.Targets;

namespace Monitoring.HealthChecks;

public class TcpCheckProvider : IHealthCheckProvider
{
    public Task<HealthCheckResult> CheckAsync(MonitoringTarget target, CancellationToken cancellationToken = default)
    {
        // TODO: Implement TCP health check logic
        return Task.FromResult(new HealthCheckResult(false, null, null, nameof(TcpCheckProvider)));
    }
}
