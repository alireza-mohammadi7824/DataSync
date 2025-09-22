using AutoMapper;
using Monitoring.Targets;

namespace Monitoring;

public class MonitoringApplicationAutoMapperProfile : Profile
{
    public MonitoringApplicationAutoMapperProfile()
    {
        CreateMap<MonitoringTarget, MonitoringTargetDto>();
        CreateMap<ServiceStatusHistory, ServiceStatusHistoryDto>();
        CreateMap<OutageWindow, OutageWindowDto>();
        CreateMap<AlertPolicy, AlertPolicyDto>();
        CreateMap<MaintenanceWindow, MaintenanceWindowDto>();
    }
}
