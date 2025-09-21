using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Monitoring.Permissions;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;

namespace Monitoring.Web.Pages.Monitoring.Targets;

[Authorize(MonitoringPermissions.Services.View)]
public class IndexModel : AbpPageModel
{
    public bool CanRun { get; private set; }

    public bool CanEdit { get; private set; }

    public bool CanDelete { get; private set; }

    public async Task OnGetAsync()
    {
        CanRun = await AuthorizationService.IsGrantedAsync(MonitoringPermissions.Services.Run);
        CanEdit = await AuthorizationService.IsGrantedAsync(MonitoringPermissions.Services.Edit);
        CanDelete = await AuthorizationService.IsGrantedAsync(MonitoringPermissions.Services.Delete);
    }
}
