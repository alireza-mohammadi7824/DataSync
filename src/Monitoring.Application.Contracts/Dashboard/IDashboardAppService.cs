using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Monitoring.Dashboard;

public interface IDashboardAppService : IApplicationService
{
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);

    Task<TargetDashboardListDto> GetTargetsAsync(TargetDashboardListInput input, CancellationToken cancellationToken = default);
}
