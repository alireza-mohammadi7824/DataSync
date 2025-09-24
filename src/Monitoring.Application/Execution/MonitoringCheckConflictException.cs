using Volo.Abp;

namespace Monitoring.Execution;

public sealed class MonitoringCheckConflictException : BusinessException
{
    public MonitoringCheckConflictException(string message = "Check is already running for this target.")
        : base("Monitoring:CheckConflict")
    {
        Details = message;
    }
}
