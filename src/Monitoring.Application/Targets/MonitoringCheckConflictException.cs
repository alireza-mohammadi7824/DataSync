using Volo.Abp;

namespace Monitoring.Targets;

public class MonitoringCheckConflictException : BusinessException
{
    public MonitoringCheckConflictException(string reason)
        : base("Monitoring:CheckConflict", reason)
    {
    }
}
