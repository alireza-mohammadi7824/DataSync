namespace Monitoring.Options;

/// <summary>
/// Options controlling retention purges.
/// Sample configuration:
/// <code>
/// "Monitoring": {
///   "Retention": {
///     "HistoryDays": 90,
///     "MaxHistoryPerTarget": 10000,
///     "PurgeBatchSize": 1000,
///     "KeepLastOutagesPerTarget": 50,
///     "ScheduleUtc": "02:30"
///   }
/// }
/// </code>
/// Environment variables are also supported, e.g.
/// <c>MONITORING__RETENTION__HISTORYDAYS</c>,
/// <c>MONITORING__RETENTION__MAXHISTORYPERTARGET</c>,
/// <c>MONITORING__RETENTION__PURGEBATCHSIZE</c>,
/// <c>MONITORING__RETENTION__KEEPLASTOUTAGESPERTARGET</c>,
/// <c>MONITORING__RETENTION__SCHEDULEUTC</c>.
/// </summary>
public sealed class MonitoringRetentionOptions
{
    public int HistoryDays { get; set; } = 90;

    public int MaxHistoryPerTarget { get; set; } = 10_000;

    public int PurgeBatchSize { get; set; } = 1_000;

    public int KeepLastOutagesPerTarget { get; set; } = 50;

    public string? ScheduleUtc { get; set; } = "02:30";
}
