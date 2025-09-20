using Microsoft.Extensions.DependencyInjection;
using Monitoring.HealthChecks;
using Volo.Abp.Authorization;
using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;

namespace Monitoring;

[DependsOn(
    typeof(MonitoringDomainModule),
    typeof(AbpAuthorizationModule),
    typeof(MonitoringApplicationContractsModule),
    typeof(AbpAutoMapperModule)
    )]
public class MonitoringApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddTransient<WebsiteCheckProvider>();
        context.Services.AddTransient<ApiCheckProvider>();
        context.Services.AddTransient<TcpCheckProvider>();
        context.Services.AddTransient<RedisCheckProvider>();
        context.Services.AddTransient<IHealthCheckProviderResolver, HealthCheckProviderResolver>();

        Configure<AbpAutoMapperOptions>(options =>
        {
            options.AddMaps<MonitoringApplicationModule>();
        });
    }
}
