using System.Threading.Tasks;
using Monitoring.Permissions;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.UI.Navigation;

namespace Monitoring.Menus;

public class MonitoringMenuContributor : IMenuContributor
{
    public async Task ConfigureMenuAsync(MenuConfigurationContext context)
    {
        if (context.Menu.Name != StandardMenus.Main)
        {
            return;
        }

        var monitoringMenu = new ApplicationMenuItem(
            MonitoringMenus.MonitoringTargets,
            displayName: "Monitoring",
            url: "/Monitoring/Targets",
            icon: "fa fa-chart-line"
        );

        if (await context.IsGrantedAsync(MonitoringPermissions.Services.View))
        {
            context.Menu.AddItem(monitoringMenu);
        }
    }
}
