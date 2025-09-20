using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace Monitoring;

[DependsOn(
    typeof(AbpDddDomainModule)
    )]
public class MonitoringDomainModule : AbpModule
{
}
