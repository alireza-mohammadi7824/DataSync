using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Monitoring.Endpoints;
using Monitoring.Targets;

namespace Monitoring.Execution.Providers;

public sealed class WebsiteCheckProvider : IHealthCheckProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebsiteCheckProvider> _logger;

    public WebsiteCheckProvider(IHttpClientFactory httpClientFactory, ILogger<WebsiteCheckProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public EndpointType Type => EndpointType.Website;

    public async Task<HealthCheckResult> RunAsync(
        MonitoringTarget target,
        ParsedEndpoint endpoint,
        string triggerSource,
        CancellationToken ct)
    {
        var timeout = TimeSpan.FromSeconds(target.TimeoutSeconds > 0 ? target.TimeoutSeconds : 15);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        var client = _httpClientFactory.CreateClient(nameof(WebsiteCheckProvider));
        client.Timeout = Timeout.InfiniteTimeSpan;

        var requestUri = BuildUri(endpoint);
        var allowedStatusCodes = SettingsReader.Get<int[]>(target.SettingsJson, "allowedStatusCodes");
        HashSet<int>? allowedSet = null;
        if (allowedStatusCodes is { Length: > 0 })
        {
            allowedSet = new HashSet<int>(allowedStatusCodes);
        }

        var methods = new[] { HttpMethod.Head, HttpMethod.Get };
        foreach (var method in methods)
        {
            using var request = new HttpRequestMessage(method, requestUri);
            var stopwatch = Stopwatch.StartNew();
            try
            {
                using var response = await client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeoutCts.Token);

                var status = (int)response.StatusCode;
                if (method == HttpMethod.Head && (status == (int)HttpStatusCode.MethodNotAllowed || status == (int)HttpStatusCode.NotImplemented))
                {
                    stopwatch.Stop();
                    // Retry with GET when HEAD not supported
                    continue;
                }

                stopwatch.Stop();
                var durationMs = ClampDuration(stopwatch.ElapsedMilliseconds);
                var success = allowedSet?.Contains(status) ?? (status >= 200 && status < 400);
                var summary = $"{method.Method.ToUpperInvariant()} {status} in {durationMs}ms";

                LogOutcome(success, endpoint, status, durationMs, triggerSource);
                return new HealthCheckResult(success, durationMs, summary, triggerSource);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested && timeoutCts.IsCancellationRequested)
            {
                stopwatch.Stop();
                var durationMs = ClampDuration(stopwatch.ElapsedMilliseconds);
                _logger.LogWarning(
                    "Website check timeout for {Host} {Port} ({Provider}) after {Duration}ms (trigger {Trigger})",
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
                    "Website check failed for {Host} {Port} ({Provider}) after {Duration}ms (trigger {Trigger})",
                    endpoint.Host,
                    ResolvePort(endpoint),
                    Type,
                    durationMs,
                    triggerSource);
                return new HealthCheckResult(false, durationMs, "Request failed", triggerSource);
            }
        }

        _logger.LogWarning(
            "Website check did not complete for {Host} {Port} ({Provider}) (trigger {Trigger})",
            endpoint.Host,
            ResolvePort(endpoint),
            Type,
            triggerSource);
        return new HealthCheckResult(false, null, "Unknown failure", triggerSource);
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

    private void LogOutcome(bool success, ParsedEndpoint endpoint, int status, int durationMs, string trigger)
    {
        var level = success ? LogLevel.Information : LogLevel.Warning;
        _logger.Log(level,
            "Website check {Status} for {Host} {Port} ({Provider}) in {Duration}ms (trigger {Trigger})",
            status,
            endpoint.Host,
            ResolvePort(endpoint),
            Type,
            durationMs,
            trigger);
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
