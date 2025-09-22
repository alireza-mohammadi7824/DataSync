using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Monitoring.Targets;

public interface IMonitoringTargetAppService : IApplicationService
{
    Task<PagedResultDto<MonitoringTargetDto>> GetListAsync(PagedAndSortedResultRequestDto input, ServiceType? type = null, string? search = null);

    Task<MonitoringTargetDto> GetAsync(Guid id);

    Task<MonitoringTargetDto> CreateAsync(CreateUpdateMonitoringTargetDto input);

    Task<MonitoringTargetDto> UpdateAsync(Guid id, CreateUpdateMonitoringTargetDto input);

    Task DeleteAsync(Guid id);

    Task TriggerCheckAsync(Guid id);

    Task<HealthCheckResultDto> CheckNowAsync(Guid id);

    Task<CheckBatchEnqueueResultDto> EnqueueCheckAllAsync();

    Task<CheckBatchStatusDto> GetCheckBatchStatusAsync(Guid batchId);

    Task<MonitoringMetricsDto> GetMetricsAsync();

    Task<List<MonitoringTargetDto>> GetOverviewAsync(ServiceType? type = null);

    Task<List<OutageWindowDto>> GetRecentOutagesAsync(Guid targetId, int count = 10);

    Task<List<ServiceStatusHistoryDto>> GetRecentStatusHistoryAsync(Guid targetId, int count = 20);

    Task<AlertPolicyDto> GetAlertPolicyAsync(Guid targetId);

    Task<AlertPolicyDto> UpsertAlertPolicyAsync(Guid targetId, AlertPolicyDto input);

    Task<List<MaintenanceWindowDto>> GetMaintenanceAsync(Guid? targetId = null);

    Task<MaintenanceWindowDto> CreateMaintenanceAsync(CreateUpdateMaintenanceWindowDto input);

    Task<MaintenanceWindowDto> UpdateMaintenanceAsync(Guid id, CreateUpdateMaintenanceWindowDto input);

    Task DeleteMaintenanceAsync(Guid id);
}
