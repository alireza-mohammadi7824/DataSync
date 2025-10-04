using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Monitoring.Dashboard;

public interface IDashboardAppService : IApplicationService
{
    Task<DashboardSummaryDto> GetSummaryAsync();

    Task<TargetDashboardListDto> GetTargetsAsync(TargetDashboardListInput input);
}
