using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Monitoring.Tasks;

public interface IMonitoringTaskAppService :
    ICrudAppService<
        MonitoringTaskDto,
        Guid,
        PagedAndSortedResultRequestDto,
        CreateUpdateMonitoringTaskDto>
{
    Task TriggerExecutionAsync(Guid id);
}
