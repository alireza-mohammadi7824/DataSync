using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monitoring.Alerts;
using Monitoring.Execution;
using Monitoring.HealthChecks;
using Monitoring.Options;
using Monitoring.Retention;
using Monitoring.Workers;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.AutoMapper;
using Volo.Abp.Authorization;
using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace Monitoring;

[DependsOn(
    typeof(MonitoringDomainModule),
    typeof(MonitoringApplicationContractsModule),
    typeof(AbpAutoMapperModule),
    typeof(AbpAuthorizationModule),
    typeof(AbpDddApplicationModule)
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
        context.Services.AddTransient<INotificationChannelResolver, NotificationChannelResolver>();
        context.Services.TryAddSingleton<ExecutionMetrics>();
        context.Services.AddSingleton<IValidateOptions<MonitoringOptions>, MonitoringOptionsValidator>();
        context.Services.AddTransient<HealthCheckExecutor>();
        context.Services.AddTransient<IMonitoringCheckService, MonitoringCheckService>();
        context.Services.AddSingleton<BulkCheckQueue>();
        context.Services.AddSingleton<IBulkCheckQueue>(sp => sp.GetRequiredService<BulkCheckQueue>());
        context.Services.AddSingleton<ITargetRunLock, DatabaseTargetRunLock>();
        context.Services.AddHostedService<MonitoringWorker>();
        context.Services.AddHostedService<BulkCheckProcessor>();
        context.Services.AddHostedService<MonitoringRetentionWorker>();

        var configuration = context.Services.GetConfiguration();
        Configure<MonitoringOptions>(configuration.GetSection("Monitoring"));
        Configure<MonitoringRetentionOptions>(configuration.GetSection("Monitoring:Retention"));
        
        Configure<AbpAutoMapperOptions>(options =>
        {
            options.AddMaps<MonitoringApplicationModule>();
        });

        context.Services.AddSingleton<IValidateOptions<MonitoringRetentionOptions>, MonitoringRetentionOptionsValidator>();
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
    }
}
