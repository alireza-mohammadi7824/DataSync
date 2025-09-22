using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Monitoring.Alerts;

public interface IAlertPolicyAppService :
    ICrudAppService<
        AlertPolicyDto,
        Guid,
        PagedAndSortedResultRequestDto,
        CreateUpdateAlertPolicyDto>
{
}
