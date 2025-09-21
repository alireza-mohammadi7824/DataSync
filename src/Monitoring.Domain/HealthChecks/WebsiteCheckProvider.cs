using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Monitoring.Targets;

namespace Monitoring.HealthChecks;

public class WebsiteCheckProvider : IHealthCheckProvider
{
    private const int DefaultSuccessMin = 200;
    private const int DefaultSuccessMax = 399;
    private const int DefaultTimeoutSeconds = 5;

    private readonly IHttpClientFactory _httpClientFactory;

    public WebsiteCheckProvider(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<HealthCheckResult> CheckAsync(
        MonitoringTarget target,
        string triggerSource,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Uri.TryCreate(target.Endpoint, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return new HealthCheckResult(false, null, "Invalid URL", triggerSource);
        }

        var settings = HealthCheckSettingsParser.ParseWebsite(target.SettingsJson);
        var timeoutSeconds = target.TimeoutSeconds > 0 ? target.TimeoutSeconds : DefaultTimeoutSeconds;

        var client = _httpClientFactory.CreateClient(nameof(WebsiteCheckProvider));
        client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

        using var request = new HttpRequestMessage(
            HttpHealthCheckHelper.ResolveMethod(settings.Method, HttpMethod.Get),
            uri);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            stopwatch.Stop();

            var responseTimeMs = (int)Math.Round(stopwatch.Elapsed.TotalMilliseconds);

            var isSuccessStatus = HttpHealthCheckHelper.IsExpectedStatus(
                response.StatusCode,
                settings.ExpectedStatusCodes,
                DefaultSuccessMin,
                DefaultSuccessMax);

            if (!isSuccessStatus)
            {
                return new HealthCheckResult(false, responseTimeMs, $"HTTP {(int)response.StatusCode}", triggerSource);
            }

            if (!string.IsNullOrWhiteSpace(settings.ContainsKeyword))
            {
                var snippet = await HttpHealthCheckHelper.ReadContentSnippetAsync(response, cancellationToken);
                if (snippet.IndexOf(settings.ContainsKeyword, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return new HealthCheckResult(false, responseTimeMs, "Keyword not found", triggerSource);
                }
            }

            return new HealthCheckResult(true, responseTimeMs, null, triggerSource);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            var elapsed = stopwatch.ElapsedMilliseconds > 0
                ? (int?)Math.Round(stopwatch.Elapsed.TotalMilliseconds)
                : null;

            return new HealthCheckResult(false, elapsed, $"Timeout {timeoutSeconds}s", triggerSource);
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            var elapsed = stopwatch.ElapsedMilliseconds > 0
                ? (int?)Math.Round(stopwatch.Elapsed.TotalMilliseconds)
                : null;

            var summary = ex.StatusCode.HasValue
                ? $"HTTP {(int)ex.StatusCode.Value}"
                : "Request failed";

            return new HealthCheckResult(false, elapsed, summary, triggerSource);
        }
        catch (Exception)
        {
            stopwatch.Stop();
            var elapsed = stopwatch.ElapsedMilliseconds > 0
                ? (int?)Math.Round(stopwatch.Elapsed.TotalMilliseconds)
                : null;

            return new HealthCheckResult(false, elapsed, "Request failed", triggerSource);
        }
    }
}
