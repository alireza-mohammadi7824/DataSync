using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Monitoring.History;

public interface IHistoryAppService : IApplicationService
{
    Task<OutageListDto> GetOutagesAsync(Guid id, int? count = null, CancellationToken cancellationToken = default);

    Task<TimelineDto> GetTimelineAsync(Guid id, TimelineRequestDto input, CancellationToken cancellationToken = default);
}
