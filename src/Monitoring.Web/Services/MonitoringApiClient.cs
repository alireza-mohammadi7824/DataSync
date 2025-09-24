using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Monitoring.Dashboard;
using Monitoring.History;
using Monitoring.Targets;

namespace Monitoring.Web.Services;

public sealed class MonitoringApiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<MonitoringApiClient> _logger;

    public MonitoringApiClient(
        IHttpClientFactory httpClientFactory,
        IHttpContextAccessor httpContextAccessor,
        ILogger<MonitoringApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<DashboardSummaryDto?> GetSummaryAsync(CancellationToken cancellationToken)
    {
        return await GetAsync<DashboardSummaryDto>("api/monitoring/dashboard/summary", cancellationToken);
    }

    public async Task<TargetDashboardListDto?> GetTargetsAsync(int skip, int take, CancellationToken cancellationToken)
    {
        var uri = $"api/monitoring/dashboard/targets?skipCount={Math.Max(0, skip)}&maxResultCount={Math.Max(1, take)}";
        return await GetAsync<TargetDashboardListDto>(uri, cancellationToken);
    }

    public async Task<List<OutageDto>?> GetOutagesAsync(Guid id, int count, CancellationToken cancellationToken)
    {
        var dto = await GetAsync<OutageListDto>($"api/monitoring/targets/{id}/outages?count={Math.Clamp(count, 1, 100)}", cancellationToken);
        return dto?.Items ?? new List<OutageDto>();
    }

    public async Task CheckNowAsync(Guid id, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/monitoring/targets/{id}/check");
        var response = await SendAsync(request, cancellationToken, allowAccepted: false);
        response?.Dispose();
    }

    public async Task<string?> CheckAllAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/monitoring/targets/check-all");
        var response = await SendAsync(request, cancellationToken, allowAccepted: true);

        if (response == null)
        {
            return null;
        }

        try
        {
            var result = await DeserializeAsync<CheckBatchEnqueueResultDto>(response, cancellationToken);
            return result?.BatchId.ToString();
        }
        finally
        {
            response.Dispose();
        }
    }

    public async Task<MonitoringMetricsSnapshot?> GetMetricsAsync(CancellationToken cancellationToken)
    {
        return await GetAsync<MonitoringMetricsSnapshot>("api/monitoring/metrics", cancellationToken);
    }

    private async Task<T?> GetAsync<T>(string relativeUrl, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
        var response = await SendAsync(request, cancellationToken, allowAccepted: false);

        if (response == null)
        {
            return default;
        }

        try
        {
            return await DeserializeAsync<T>(response, cancellationToken);
        }
        finally
        {
            response.Dispose();
        }
    }

    private async Task<HttpResponseMessage?> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken, bool allowAccepted)
    {
        var client = CreateClient();
        HttpResponseMessage response;

        try
        {
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Monitoring API request to {Path} failed.", request.RequestUri?.ToString() ?? request.RequestUri);
            throw new MonitoringApiClientException("The monitoring API request failed.", ex);
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            response.Dispose();
            throw new MonitoringApiClientException("You are not authorized to perform this action.");
        }

        if ((int)response.StatusCode == 429)
        {
            response.Dispose();
            throw new MonitoringApiClientException("The monitoring API is temporarily rate limited. Please try again shortly.");
        }

        if (allowAccepted && response.StatusCode == HttpStatusCode.Accepted)
        {
            return response;
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var conflictMessage = await TryReadMessageAsync(response, cancellationToken);
            response.Dispose();
            throw new MonitoringApiClientException(conflictMessage ?? "The operation could not be completed because a related check is already running.");
        }

        var errorMessage = await TryReadMessageAsync(response, cancellationToken);
        response.Dispose();
        throw new MonitoringApiClientException(errorMessage ?? $"Request failed with status code {(int)response.StatusCode}.");
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient(nameof(MonitoringApiClient));
        if (client.BaseAddress != null)
        {
            return client;
        }

        var request = _httpContextAccessor.HttpContext?.Request;
        if (request == null)
        {
            throw new MonitoringApiClientException("No active HTTP context was found for the monitoring request.");
        }

        var host = request.Host.HasValue ? request.Host.Value : string.Empty;
        var pathBase = request.PathBase.HasValue ? request.PathBase.Value : string.Empty;
        var baseUrl = string.IsNullOrEmpty(host)
            ? throw new MonitoringApiClientException("Unable to resolve the application base URL.")
            : $"{request.Scheme}://{host}{pathBase}";

        if (!baseUrl.EndsWith('/'))
        {
            baseUrl += "/";
        }

        client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
        return client;
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, SerializerOptions, cancellationToken);
    }

    private static async Task<string?> TryReadMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            if (stream == null)
            {
                return null;
            }

            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (document.RootElement.TryGetProperty("error", out var errorElement) && errorElement.TryGetProperty("message", out var messageElement))
            {
                return messageElement.GetString();
            }

            if (document.RootElement.TryGetProperty("message", out var message))
            {
                return message.GetString();
            }
        }
        catch
        {
            // ignored
        }

        return null;
    }
}

public sealed class MonitoringApiClientException : Exception
{
    public MonitoringApiClientException(string message)
        : base(message)
    {
    }

    public MonitoringApiClientException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class MonitoringMetricsSnapshot
{
    public DateTime GeneratedAtUtc { get; set; }

    public long ChecksStarted { get; set; }

    public long ChecksSucceeded { get; set; }

    public long ChecksFailed { get; set; }

    public long ChecksSkipped { get; set; }

    public long LocksContended { get; set; }
}
