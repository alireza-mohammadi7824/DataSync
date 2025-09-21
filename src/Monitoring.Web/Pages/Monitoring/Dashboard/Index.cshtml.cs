using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Monitoring.Options;
using Monitoring.Permissions;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;

namespace Monitoring.Web.Pages.Monitoring.Dashboard;

[Authorize(MonitoringPermissions.Services.View)]
public class IndexModel : AbpPageModel
{
    public int DefaultRangeDays { get; }

    public int MaxRangeDays { get; }

    public IndexModel(IOptions<MonitoringOptions> options)
    {
        var dashboard = options.Value.Dashboard;
        DefaultRangeDays = dashboard.DefaultRangeDays <= 0 ? 7 : dashboard.DefaultRangeDays;
        MaxRangeDays = dashboard.MaxRangeDays <= 0 ? DefaultRangeDays : dashboard.MaxRangeDays;
        if (MaxRangeDays < DefaultRangeDays)
        {
            MaxRangeDays = DefaultRangeDays;
        }
    }
}
