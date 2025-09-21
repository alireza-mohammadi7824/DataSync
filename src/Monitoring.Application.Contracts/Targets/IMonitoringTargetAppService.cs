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
}
