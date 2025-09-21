using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Modularity;

namespace Monitoring;

[DependsOn(
    typeof(MonitoringApplicationContractsModule),
    typeof(AbpAspNetCoreMvcModule)
    )]
public class MonitoringHttpApiModule : AbpModule
{
}
