using System;
using Volo.Abp.Application.Dtos;

namespace Monitoring.Dashboard;

public class DashboardSummaryDto : EntityDto<Guid>
{
    public int TotalTargets { get; set; }

    public int OnlineCount { get; set; }

    public int OfflineCount { get; set; }

    public int CheckingCount { get; set; }

    public double UptimePercentage { get; set; }

    public int IncidentsCount { get; set; }

    public DateTime RangeStart { get; set; }

    public DateTime RangeEnd { get; set; }

    public DashboardSummaryDto()
    {
        Id = Guid.Empty;
    }
}
