using Volo.Abp;

namespace Monitoring.Execution;

public class MonitoringCheckConflictException : BusinessException
{
    public MonitoringCheckConflictException(string message = "Monitoring check conflict.")
        : base("Monitoring:CheckConflict", message)
    {
    }
}
