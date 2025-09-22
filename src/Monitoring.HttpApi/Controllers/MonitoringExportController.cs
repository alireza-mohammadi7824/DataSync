using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Monitoring.Dashboard;
using Monitoring.Permissions;

namespace Monitoring.Controllers;

[Route("api/monitoring/export")]
[Authorize(MonitoringPermissions.Services.View)]
[EnableRateLimiting("monitoring-read")]
public class MonitoringExportController : MonitoringController
{
    private readonly IDashboardAppService _dashboardAppService;

    public MonitoringExportController(IDashboardAppService dashboardAppService)
    {
        _dashboardAppService = dashboardAppService;
    }

    [HttpGet("uptime/{id}.csv")]
    public async Task<IActionResult> ExportUptimeAsync(Guid id, CancellationToken cancellationToken)
    {
        var target = await FindTargetAsync(id, cancellationToken);
        if (target == null)
        {
            return NotFound();
        }

        var builder = new StringBuilder();
        builder.AppendLine("window,uptimePercentage");
        builder.AppendLine(string.Join(',', "24h", Escape(target.Uptime24h.ToString("0.##"))));
        builder.AppendLine(string.Join(',', "7d", Escape(target.Uptime7d.ToString("0.##"))));
        builder.AppendLine(string.Join(',', "30d", Escape(target.Uptime30d.ToString("0.##"))));

        return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", $"uptime-{id}.csv");
    }

    [HttpGet("summary.csv")]
    public async Task<FileContentResult> ExportSummaryAsync(CancellationToken cancellationToken)
    {
        var summary = await _dashboardAppService.GetSummaryAsync(cancellationToken);
        var builder = new StringBuilder();
        builder.AppendLine("metric,value");
        builder.AppendLine(string.Join(',', "uptime24h", Escape(summary.Uptime24h.ToString("0.##"))));
        builder.AppendLine(string.Join(',', "uptime7d", Escape(summary.Uptime7d.ToString("0.##"))));
        builder.AppendLine(string.Join(',', "uptime30d", Escape(summary.Uptime30d.ToString("0.##"))));
        builder.AppendLine(string.Join(',', "mttr30d", Escape(summary.Mttr30d.ToString("0.##"))));
        builder.AppendLine(string.Join(',', "mtbf30d", Escape(summary.Mtbf30d.ToString("0.##"))));
        builder.AppendLine(string.Join(',', "online", summary.OnlineCount.ToString()));
        builder.AppendLine(string.Join(',', "offline", summary.OfflineCount.ToString()));
        builder.AppendLine(string.Join(',', "checking", summary.CheckingCount.ToString()));

        return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", "dashboard-summary.csv");
    }

    private async Task<TargetDashboardItemDto?> FindTargetAsync(Guid id, CancellationToken cancellationToken)
    {
        const int pageSize = 200;
        var skip = 0;

        while (true)
        {
            var result = await _dashboardAppService.GetTargetsAsync(
                new TargetDashboardListInput
                {
                    SkipCount = skip,
                    MaxResultCount = pageSize,
                    Sorting = "name"
                },
                cancellationToken);

            if (result.Items == null || result.Items.Count == 0)
            {
                return null;
            }

            var match = result.Items.FirstOrDefault(item => item.Id == id);
            if (match != null)
            {
                return match;
            }

            skip += pageSize;
            if (skip >= result.TotalCount)
            {
                return null;
            }
        }
    }

    private static string Escape(string value)
    {
        value ??= string.Empty;
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
