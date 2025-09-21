using System.Threading;
using System.Threading.Tasks;
using Monitoring.Targets;

namespace Monitoring.HealthChecks;

public record HealthCheckResult(
    bool IsSuccess,
    int? ResponseTimeMs,
    string? ErrorSummary,
    string TriggerSource);

public interface IHealthCheckProvider
{
    Task<HealthCheckResult> CheckAsync(
        MonitoringTarget target,
        string triggerSource,
        CancellationToken cancellationToken = default);
}

public interface IHealthCheckProviderResolver
{
    IHealthCheckProvider Resolve(ServiceType type);
}
