using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Monitoring.Endpoints;
using Monitoring.Targets;
using Monitoring.Shared;

namespace Monitoring.Execution.Providers;

public sealed class ApiCheckProvider : IHealthCheckProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApiCheckProvider> _logger;

    public ApiCheckProvider(IHttpClientFactory httpClientFactory, ILogger<ApiCheckProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public EndpointType Type => EndpointType.Api;

    public async Task<HealthCheckResult> RunAsync(
        MonitoringTarget target,
        ParsedEndpoint endpoint,
        string triggerSource,
        CancellationToken ct)
    {
        var timeout = TimeSpan.FromSeconds(target.TimeoutSeconds > 0 ? target.TimeoutSeconds : 15);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        var client = _httpClientFactory.CreateClient(nameof(ApiCheckProvider));
        client.Timeout = Timeout.InfiniteTimeSpan;

        var requestUri = BuildUri(endpoint);
        var method = (SettingsReader.Get<string>(target.SettingsJson, "method") ?? "GET").ToUpperInvariant();
        var expectedStatus = SettingsReader.Get<int?>(target.SettingsJson, "expectedStatusCode", 200) ?? 200;
        var payload = SettingsReader.Get(target.SettingsJson, "payload");
        var jsonPath = SettingsReader.Get<string>(target.SettingsJson, "jsonPath");
        var expectedJson = SettingsReader.Get(target.SettingsJson, "equals");

        using var request = new HttpRequestMessage(new HttpMethod(method), requestUri);
        if (string.Equals(method, HttpMethod.Post.Method, StringComparison.OrdinalIgnoreCase) && payload.HasValue)
        {
            var json = payload.Value.GetRawText();
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
            stopwatch.Stop();
            var durationMs = ClampDuration(stopwatch.ElapsedMilliseconds);
            var statusCode = (int)response.StatusCode;
            var summary = $"{method} {statusCode} in {durationMs}ms";

            if (statusCode != expectedStatus)
            {
                _logger.LogWarning(
                    "API check unexpected status {Status} for {Host} {Port} ({Provider}) in {Duration}ms (trigger {Trigger})",
                    statusCode,
                    endpoint.Host,
                    ResolvePort(endpoint),
                    Type,
                    durationMs,
                    triggerSource);
                return new HealthCheckResult(false, durationMs, summary, triggerSource);
            }

            if (!string.IsNullOrWhiteSpace(jsonPath))
            {
                var content = await response.Content.ReadAsStringAsync(timeoutCts.Token);
                var element = SettingsReader.Get(content, jsonPath);
                if (element is null)
                {
                    _logger.LogWarning(
                        "API check missing JSON path {Path} for {Host} {Port} ({Provider}) in {Duration}ms (trigger {Trigger})",
                        jsonPath,
                        endpoint.Host,
                        ResolvePort(endpoint),
                        Type,
                        durationMs,
                        triggerSource);
                    return new HealthCheckResult(false, durationMs, $"JSON path {jsonPath} missing", triggerSource);
                }

                if (expectedJson.HasValue)
                {
                    if (!element.Value.DeepEquals(expectedJson.Value))
                    {
                        _logger.LogWarning(
                            "API check JSON mismatch at {Path} for {Host} {Port} ({Provider}) in {Duration}ms (trigger {Trigger})",
                            jsonPath,
                            endpoint.Host,
                            ResolvePort(endpoint),
                            Type,
                            durationMs,
                            triggerSource);
                        return new HealthCheckResult(false, durationMs, $"JSON path {jsonPath} mismatch", triggerSource);
                    }
                }
            }

            _logger.LogInformation(
                "API check {Status} for {Host} {Port} ({Provider}) in {Duration}ms (trigger {Trigger})",
                statusCode,
                endpoint.Host,
                ResolvePort(endpoint),
                Type,
                durationMs,
                triggerSource);
            return new HealthCheckResult(true, durationMs, summary, triggerSource);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            stopwatch.Stop();
            var durationMs = ClampDuration(stopwatch.ElapsedMilliseconds);
            _logger.LogWarning(
                "API check timeout for {Host} {Port} ({Provider}) after {Duration}ms (trigger {Trigger})",
                endpoint.Host,
                ResolvePort(endpoint),
                Type,
                durationMs,
                triggerSource);
            return new HealthCheckResult(false, durationMs, $"Timeout {Math.Round(timeout.TotalSeconds)}s", triggerSource);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            stopwatch.Stop();
            var durationMs = ClampDuration(stopwatch.ElapsedMilliseconds);
            _logger.LogWarning(
                ex,
                "API check failed for {Host} {Port} ({Provider}) after {Duration}ms (trigger {Trigger})",
                endpoint.Host,
                ResolvePort(endpoint),
                Type,
                durationMs,
                triggerSource);
            return new HealthCheckResult(false, durationMs, "Request failed", triggerSource);
        }
    }

    private static Uri BuildUri(ParsedEndpoint endpoint)
    {
        var scheme = string.IsNullOrEmpty(endpoint.Scheme) ? "https" : endpoint.Scheme!;
        var builder = new UriBuilder(scheme, endpoint.Host);
        if (endpoint.Port.HasValue)
        {
            builder.Port = endpoint.Port.Value;
        }

        var pathAndQuery = string.IsNullOrEmpty(endpoint.PathAndQuery) ? "/" : endpoint.PathAndQuery!;
        var questionIndex = pathAndQuery.IndexOf('?', StringComparison.Ordinal);
        if (questionIndex >= 0)
        {
            builder.Path = pathAndQuery[..questionIndex];
            builder.Query = pathAndQuery[(questionIndex + 1)..];
        }
        else
        {
            builder.Path = pathAndQuery;
            builder.Query = string.Empty;
        }

        return builder.Uri;
    }

    private static int ClampDuration(long elapsed)
    {
        if (elapsed <= 0)
        {
            return 0;
        }

        return (int)Math.Min(elapsed, int.MaxValue);
    }

    private static int? ResolvePort(ParsedEndpoint endpoint)
    {
        if (endpoint.Port.HasValue)
        {
            return endpoint.Port.Value;
        }

        if (string.Equals(endpoint.Scheme, "http", StringComparison.OrdinalIgnoreCase))
        {
            return 80;
        }

        if (string.Equals(endpoint.Scheme, "https", StringComparison.OrdinalIgnoreCase))
        {
            return 443;
        }

        return null;
    }
}
