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
            MonitoringMenus.MonitoringTasks,
            displayName: "Monitoring",
            url: "/Monitoring/Tasks",
            icon: "fa fa-chart-line"
        );

        if (await context.IsGrantedAsync(MonitoringPermissions.View))
        {
            context.Menu.AddItem(monitoringMenu);
        }
    }
}
