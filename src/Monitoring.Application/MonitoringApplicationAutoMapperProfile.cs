using AutoMapper;
using Monitoring.Tasks;

namespace Monitoring;

public class MonitoringApplicationAutoMapperProfile : Profile
{
    public MonitoringApplicationAutoMapperProfile()
    {
        CreateMap<MonitoringTask, MonitoringTaskDto>();
    }
}
