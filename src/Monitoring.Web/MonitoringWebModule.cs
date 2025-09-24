using Microsoft.Extensions.DependencyInjection;
using Monitoring.Menus;
using Monitoring.Web.Services;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;
using Volo.Abp.UI.Navigation;

namespace Monitoring;

[DependsOn(
    typeof(MonitoringHttpApiModule),
    typeof(MonitoringApplicationModule),
    typeof(AbpAutoMapperModule),
    typeof(AbpAspNetCoreMvcUiThemeSharedModule)
    )]
public class MonitoringWebModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddHttpClient<MonitoringApiClient>();

        Configure<AbpNavigationOptions>(options =>
        {
            options.MenuContributors.Add(new MonitoringMenuContributor());
        });

        Configure<AbpAutoMapperOptions>(options =>
        {
            options.AddMaps<MonitoringWebModule>();
        });
    }
}
