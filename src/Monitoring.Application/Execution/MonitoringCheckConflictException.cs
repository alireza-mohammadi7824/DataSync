using Volo.Abp;

namespace Monitoring.Execution;

public sealed class MonitoringCheckConflictException : BusinessException
{
    public MonitoringCheckConflictException(string? details = null)
        : base("Monitoring:CheckConflict")
    {
        Details = details ?? "Check is already running for this target.";
    }
}
