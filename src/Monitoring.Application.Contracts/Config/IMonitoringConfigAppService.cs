using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Monitoring.Config;

public interface IMonitoringConfigAppService : IApplicationService
{
    Task<MonitoringConfigDto> GetAsync();
}
