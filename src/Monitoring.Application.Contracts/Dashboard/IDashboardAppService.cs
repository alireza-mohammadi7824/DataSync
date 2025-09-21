using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Monitoring.Targets;
using Volo.Abp.Application.Services;

namespace Monitoring.Dashboard;

public interface IDashboardAppService : IApplicationService
{
    Task<DashboardSummaryDto> GetSummaryAsync(DateTime from, DateTime to, ServiceType? filterType = null);

    Task<List<UptimeBucketDto>> GetUptimeSeriesAsync(Guid targetId, DateTime from, DateTime to, string bucket = "day");

    Task<List<DashboardIncidentDto>> GetIncidentsAsync(Guid targetId, DateTime from, DateTime to, int skip = 0, int max = 100);

    Task<MttrMtbfDto> GetReliabilityAsync(DateTime from, DateTime to, ServiceType? filterType = null);
}
