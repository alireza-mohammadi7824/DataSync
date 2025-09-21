using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Monitoring.Targets;

public interface IMonitoringTargetAppService :
    ICrudAppService<
        MonitoringTargetDto,
        Guid,
        PagedAndSortedResultRequestDto,
        CreateUpdateMonitoringTargetDto>
{
    Task TriggerCheckAsync(Guid id);

    Task<HealthCheckResultDto> CheckNowAsync(Guid id);

    Task<List<HealthCheckResultDto>> CheckAllAsync();

    Task<int> CheckAllNowAsync();

    Task<List<MonitoringTargetDto>> GetOverviewAsync(ServiceType? type = null);

    Task<List<OutageWindowDto>> GetRecentOutagesAsync(Guid targetId, int count = 10);

    Task<List<ServiceStatusHistoryDto>> GetRecentStatusHistoryAsync(Guid targetId, int count = 20);
}
