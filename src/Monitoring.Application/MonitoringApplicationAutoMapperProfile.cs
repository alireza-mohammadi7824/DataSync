using AutoMapper;
using Monitoring.Alerts;
using Monitoring.Targets;

namespace Monitoring;

public class MonitoringApplicationAutoMapperProfile : Profile
{
    public MonitoringApplicationAutoMapperProfile()
    {
        CreateMap<MonitoringTarget, MonitoringTargetDto>();
        CreateMap<ServiceStatusHistory, ServiceStatusHistoryDto>();
        CreateMap<OutageWindow, OutageWindowDto>();
        CreateMap<MaintenanceWindow, MaintenanceWindowDto>();
        CreateMap<AlertPolicy, AlertPolicyDto>()
            .ForMember(dest => dest.Emails, opt => opt.Ignore());
    }
}
