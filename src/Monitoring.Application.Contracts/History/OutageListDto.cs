using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace Monitoring.History;

public sealed class OutageListDto : ListResultDto<OutageDto>
{
    public OutageListDto()
    {
    }

    public OutageListDto(IReadOnlyList<OutageDto> items)
        : base(items)
    {
    }
}
