namespace Monitoring.Permissions;

public static class MonitoringPermissions
{
    public const string GroupName = "Monitoring";

    public static class Services
    {
        public const string Default = GroupName + ".Services";
        public const string View = GroupName + ".Services.View";
        public const string Create = GroupName + ".Services.Create";
        public const string Edit = GroupName + ".Services.Edit";
        public const string Delete = GroupName + ".Services.Delete";
        public const string Run = GroupName + ".Services.Run";
    }

    public static class Dashboard
    {
        public const string Default = GroupName + ".Dashboard";
        public const string View = Default + ".View";
    }
}
