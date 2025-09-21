using Monitoring.Localization;
using Volo.Abp.Application.Services;

namespace Monitoring;

public abstract class MonitoringAppService : ApplicationService
{
    protected MonitoringAppService()
    {
        LocalizationResource = typeof(MonitoringResource);
        ObjectMapperContext = typeof(MonitoringApplicationModule);
    }
}
