using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monitoring.HealthChecks;
using Volo.Abp.AutoMapper;
using Volo.Abp.Authorization;
using Volo.Abp.Modularity;

namespace Monitoring;

[DependsOn(
    typeof(MonitoringDomainModule),
    typeof(MonitoringApplicationContractsModule),
    typeof(AbpAutoMapperModule),
    typeof(AbpAuthorizationModule)
    )]
public class MonitoringApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddHttpClient();
        context.Services.TryAddSingleton<ISecretResolver, EnvironmentSecretResolver>();
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
