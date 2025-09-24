using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Monitoring.Observability;

public interface IMonitoringMetricsAppService : IApplicationService
{
    Task<object> GetAsync(CancellationToken cancellationToken = default);
}
