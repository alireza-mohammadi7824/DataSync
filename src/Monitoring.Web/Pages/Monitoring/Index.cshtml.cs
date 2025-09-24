using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Monitoring.Permissions;
using Monitoring.Web.Services;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;

namespace Monitoring.Web.Pages.Monitoring;

[Authorize(MonitoringPermissions.Dashboard.View)]
public class IndexModel : AbpPageModel
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly MonitoringApiClient _apiClient;

    public IndexModel(MonitoringApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public bool CanRunChecks { get; private set; }

    public bool CanViewHistory { get; private set; }

    public bool CanViewMetrics { get; private set; }

    public string? SummaryJson { get; private set; }

    public string? TargetsJson { get; private set; }

    public string? MetricsJson { get; private set; }

    public async Task OnGetAsync()
    {
        CanRunChecks = await AuthorizationService.IsGrantedAsync(MonitoringPermissions.Services.Run);
        CanViewHistory = await AuthorizationService.IsGrantedAsync(MonitoringPermissions.History.View);
        CanViewMetrics = await AuthorizationService.IsGrantedAsync(MonitoringPermissions.Metrics.View);

        var cancellationToken = HttpContext?.RequestAborted ?? CancellationToken.None;

        SummaryJson = await SerializeAsync(() => _apiClient.GetSummaryAsync(cancellationToken));
        TargetsJson = await SerializeAsync(() => _apiClient.GetTargetsAsync(0, 50, cancellationToken));

        if (CanViewMetrics)
        {
            MetricsJson = await SerializeAsync(() => _apiClient.GetMetricsAsync(cancellationToken));
        }
    }

    private static string SerializeValue<T>(T value)
    {
        return JsonSerializer.Serialize(value, SerializerOptions);
    }

    private static async Task<string?> SerializeAsync<T>(Func<Task<T?>> fetch)
    {
        try
        {
            var result = await fetch();
            return result == null ? null : SerializeValue(result);
        }
        catch (MonitoringApiClientException)
        {
            return null;
        }
    }
}
