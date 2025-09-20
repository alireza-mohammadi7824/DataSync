using Monitoring.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace Monitoring.Permissions;

public class MonitoringPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var monitoringGroup = context.AddGroup(MonitoringPermissions.GroupName, L("Permission:Monitoring"));

        var monitoringTasks = monitoringGroup.AddPermission(MonitoringPermissions.View, L("Permission:Monitoring.View"));

        monitoringTasks.AddChild(MonitoringPermissions.Create, L("Permission:Monitoring.Create"));
        monitoringTasks.AddChild(MonitoringPermissions.Edit, L("Permission:Monitoring.Edit"));
        monitoringTasks.AddChild(MonitoringPermissions.Delete, L("Permission:Monitoring.Delete"));
        monitoringTasks.AddChild(MonitoringPermissions.Run, L("Permission:Monitoring.Run"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<MonitoringResource>(name);
    }
}
