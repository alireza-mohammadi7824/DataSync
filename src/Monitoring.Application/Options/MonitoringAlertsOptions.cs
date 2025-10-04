namespace Monitoring.Options;

/// <summary>
/// Global alerting configuration that can be overridden per-target or via
/// policies. Values bind from <c>Monitoring:Alerts</c>.
/// </summary>
public sealed class MonitoringAlertsOptions
{
    public int DefaultCooldownSeconds { get; set; } = 300;
}
