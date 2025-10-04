using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace Monitoring.Dashboard;

public class TargetDashboardListDto : PagedResultDto<TargetDashboardItemDto>
{
    public TargetDashboardListDto()
    {
    }

    public TargetDashboardListDto(long totalCount, IReadOnlyList<TargetDashboardItemDto> items)
        : base(totalCount, items)
    {
    }
}
