using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitoring.Dashboard;
using Monitoring.Permissions;

namespace Monitoring.Controllers;

[Route("api/monitoring/export")]
[Authorize(MonitoringPermissions.Services.View)]
public class MonitoringExportController : MonitoringController
{
    private readonly IDashboardAppService _dashboardAppService;

    public MonitoringExportController(IDashboardAppService dashboardAppService)
    {
        _dashboardAppService = dashboardAppService;
    }

    [HttpGet("uptime/{id}.csv")]
    public async Task<FileContentResult> ExportUptimeAsync(Guid id, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string bucket = "day")
    {
        var series = await _dashboardAppService.GetUptimeSeriesAsync(id, from ?? default, to ?? default, bucket);
        var builder = new StringBuilder();
        builder.AppendLine("start,end,uptimePercentage");
        foreach (var item in series)
        {
            builder.AppendLine(string.Join(',',
                Escape(item.Start.ToString("O")),
                Escape(item.End.ToString("O")),
                Escape(item.UptimePercentage.ToString("0.##"))));
        }

        return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", $"uptime-{id}.csv");
    }

    [HttpGet("incidents/{id}.csv")]
    public async Task<FileContentResult> ExportIncidentsAsync(Guid id, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var incidents = await _dashboardAppService.GetIncidentsAsync(id, from ?? default, to ?? default, 0, 10_000);
        var builder = new StringBuilder();
        builder.AppendLine("id,start,end,durationSeconds,failureCount");
        foreach (var incident in incidents)
        {
            builder.AppendLine(string.Join(',',
                Escape(incident.Id.ToString()),
                Escape(incident.StartedAt.ToString("O")),
                Escape(incident.EndedAt?.ToString("O") ?? string.Empty),
                Escape((incident.TotalDurationSec ?? 0).ToString()),
                Escape(incident.FailureCount.ToString())));
        }

        return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", $"incidents-{id}.csv");
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
