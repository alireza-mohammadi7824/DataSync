using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Monitoring.Targets;

namespace Monitoring.HealthChecks;

public class ApiCheckProvider : IHealthCheckProvider
{
    private const int DefaultSuccessMin = 200;
    private const int DefaultSuccessMax = 299;
    private const int DefaultTimeoutSeconds = 5;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISecretResolver _secretResolver;

    public ApiCheckProvider(
        IHttpClientFactory httpClientFactory,
        ISecretResolver secretResolver)
    {
        _httpClientFactory = httpClientFactory;
        _secretResolver = secretResolver;
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

        var settings = HealthCheckSettingsParser.ParseApi(target.SettingsJson);
        var timeoutSeconds = target.TimeoutSeconds > 0 ? target.TimeoutSeconds : DefaultTimeoutSeconds;

        var client = _httpClientFactory.CreateClient(nameof(ApiCheckProvider));
        client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

        using var request = new HttpRequestMessage(
            HttpHealthCheckHelper.ResolveMethod(settings.Method, HttpMethod.Get),
            uri);

        if (!string.IsNullOrWhiteSpace(settings.Body))
        {
            request.Content = new StringContent(settings.Body, Encoding.UTF8, "application/json");
        }

        ApplyHeaders(settings, request);

        var authResult = ApplyAuthentication(settings, request, triggerSource);
        if (authResult is not null)
        {
            return authResult;
        }

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

    private static void ApplyHeaders(ApiSettings settings, HttpRequestMessage request)
    {
        if (settings.Headers is null)
        {
            return;
        }

        foreach (var header in settings.Headers)
        {
            if (string.IsNullOrWhiteSpace(header.Key))
            {
                continue;
            }

            if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value))
            {
                request.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }
    }

    private HealthCheckResult? ApplyAuthentication(
        ApiSettings settings,
        HttpRequestMessage request,
        string triggerSource)
    {
        if (settings.Auth is null || string.IsNullOrWhiteSpace(settings.Auth.Scheme))
        {
            return null;
        }

        var scheme = settings.Auth.Scheme.Trim();

        if (string.Equals(scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
        {
            var token = _secretResolver.Resolve(settings.Auth.TokenRef);
            if (string.IsNullOrWhiteSpace(token))
            {
                return new HealthCheckResult(false, null, "Auth token missing", triggerSource);
            }

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else if (string.Equals(scheme, "Basic", StringComparison.OrdinalIgnoreCase))
        {
            var username = _secretResolver.Resolve(settings.Auth.UsernameRef);
            var password = _secretResolver.Resolve(settings.Auth.PasswordRef);

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                return new HealthCheckResult(false, null, "Auth credentials missing", triggerSource);
            }

            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }

        return null;
    }
}
