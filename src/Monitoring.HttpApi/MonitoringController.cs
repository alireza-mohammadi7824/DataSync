using Monitoring.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace Monitoring;

public abstract class MonitoringController : AbpControllerBase
{
    protected MonitoringController()
    {
        LocalizationResource = typeof(MonitoringResource);
    }
}
