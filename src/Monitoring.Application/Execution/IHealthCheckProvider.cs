using System.Threading;
using System.Threading.Tasks;
using Monitoring.Endpoints;
using Monitoring.Targets;

namespace Monitoring.Execution;

public interface IHealthCheckProvider
{
    EndpointType Type { get; }

    Task<HealthCheckResult> RunAsync(
        MonitoringTarget target,
        ParsedEndpoint endpoint,
        string triggerSource,
        CancellationToken ct);
}
