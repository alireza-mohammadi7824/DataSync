using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monitoring.Alerts;
using Monitoring.Execution;
using Monitoring.Execution.Providers;
using Monitoring.Options;
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
        context.Services.AddTransient<IHealthCheckProvider, WebsiteCheckProvider>();
        context.Services.AddTransient<IHealthCheckProvider, ApiCheckProvider>();
        context.Services.AddTransient<IHealthCheckProvider, TcpCheckProvider>();
        context.Services.AddTransient<IHealthCheckProvider, RedisCheckProvider>();
        context.Services.AddSingleton<HealthCheckProviderResolver>();
        context.Services.AddTransient<INotificationChannel, EmailNotificationChannel>();
        context.Services.AddTransient<INotificationChannel, WebhookNotificationChannel>();
        context.Services.AddSingleton<INotificationChannelResolver, NotificationChannelResolver>();
        context.Services.AddSingleton<AlertThrottleStore>();
        context.Services.AddTransient<AlertEvaluator>();
        context.Services.AddTransient<AlertDispatcher>();
        context.Services.TryAddSingleton<ExecutionMetrics>();
        context.Services.AddSingleton<IValidateOptions<MonitoringOptions>, MonitoringOptionsValidator>();
        context.Services.AddTransient<HealthCheckExecutor>();
        context.Services.AddTransient<IMonitoringCheckService, MonitoringCheckService>();
        context.Services.AddSingleton<MonitoringRetentionManager>();
        context.Services.AddSingleton<BulkCheckQueue>();
        context.Services.AddSingleton<IBulkCheckQueue>(sp => sp.GetRequiredService<BulkCheckQueue>());
        context.Services.AddSingleton<ITargetRunLock, DatabaseTargetRunLock>();
        context.Services.AddHostedService<MonitoringWorker>();
        context.Services.AddHostedService<BulkCheckProcessor>();
        context.Services.AddHostedService<PurgeWorker>();

        var configuration = context.Services.GetConfiguration();
        Configure<MonitoringOptions>(configuration.GetSection("Monitoring"));

        Configure<AbpAutoMapperOptions>(options =>
        {
            options.AddMaps<MonitoringApplicationModule>();
        });
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
    }
}
