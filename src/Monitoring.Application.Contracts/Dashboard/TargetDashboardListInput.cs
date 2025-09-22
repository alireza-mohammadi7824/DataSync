using Volo.Abp.Application.Dtos;

namespace Monitoring.Dashboard;

public sealed class TargetDashboardListInput : PagedAndSortedResultRequestDto
{
    public string? ServiceType { get; set; }
    public TargetStatusDto? Status { get; set; }
}
