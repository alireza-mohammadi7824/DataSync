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

        if (!await context.IsGrantedAsync(MonitoringPermissions.Services.View))
        {
            return;
        }

        var monitoringMenu = new ApplicationMenuItem(
            MonitoringMenus.Monitoring,
            displayName: "Monitoring",
            icon: "fa fa-chart-line"
        );

        monitoringMenu.AddItem(new ApplicationMenuItem(
            MonitoringMenus.MonitoringDashboard,
            displayName: "Dashboard",
            url: "/Monitoring/Dashboard"
        ));

        monitoringMenu.AddItem(new ApplicationMenuItem(
            MonitoringMenus.MonitoringTargets,
            displayName: "Services",
            url: "/Monitoring/Targets"
        ));

        context.Menu.AddItem(monitoringMenu);
    }
}
