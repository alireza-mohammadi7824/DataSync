using Monitoring.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace Monitoring.Permissions;

public class MonitoringPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var monitoringGroup = context.AddGroup(MonitoringPermissions.GroupName, L("Permission:Monitoring"));

        var services = monitoringGroup.AddPermission(MonitoringPermissions.Services.Default, L("Permission:Monitoring.Services"));

        services.AddChild(MonitoringPermissions.Services.View, L("Permission:Monitoring.Services.View"));
        services.AddChild(MonitoringPermissions.Services.Create, L("Permission:Monitoring.Services.Create"));
        services.AddChild(MonitoringPermissions.Services.Edit, L("Permission:Monitoring.Services.Edit"));
        services.AddChild(MonitoringPermissions.Services.Delete, L("Permission:Monitoring.Services.Delete"));
        services.AddChild(MonitoringPermissions.Services.Run, L("Permission:Monitoring.Services.Run"));

        var dashboard = monitoringGroup.AddPermission(MonitoringPermissions.Dashboard.Default, L("Permission:Monitoring.Dashboard"));
        dashboard.AddChild(MonitoringPermissions.Dashboard.View, L("Permission:Monitoring.Dashboard.View"));

        var history = monitoringGroup.AddPermission(MonitoringPermissions.History.Default, L("Permission:Monitoring.History"));
        history.AddChild(MonitoringPermissions.History.View, L("Permission:Monitoring.History.View"));

        var config = monitoringGroup.AddPermission(MonitoringPermissions.Config.Default, L("Permission:Monitoring.Config"));
        config.AddChild(MonitoringPermissions.Config.View, L("Permission:Monitoring.Config.View"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<MonitoringResource>(name);
    }
}
