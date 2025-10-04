using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace Monitoring.History;

public sealed class TimelineDto : ListResultDto<TimelineIntervalDto>
{
    public TimelineDto()
    {
    }

    public TimelineDto(IReadOnlyList<TimelineIntervalDto> items)
        : base(items)
    {
    }
}
