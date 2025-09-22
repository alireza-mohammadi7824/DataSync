using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Monitoring.Alerts;

public sealed class WebhookNotificationChannel : INotificationChannel
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookNotificationChannel> _logger;

    public WebhookNotificationChannel(
        IHttpClientFactory httpClientFactory,
        ILogger<WebhookNotificationChannel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string Name => "webhook";

    public async Task SendAsync(AlertDispatch dispatch, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dispatch.Destination))
        {
            _logger.LogDebug("Skipping webhook alert because no destination was provided.");
            return;
        }

        if (!Uri.TryCreate(dispatch.Destination, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            _logger.LogWarning("Skipping webhook alert because the destination URL is invalid.");
            return;
        }
        using var client = _httpClientFactory.CreateClient("monitoring-alert-webhook");
        client.Timeout = RequestTimeout;

        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(dispatch.Payload, Encoding.UTF8, "application/json")
        };

        try
        {
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Webhook alert to host {Host} responded with status {StatusCode}.",
                    uri.Host,
                    (int)response.StatusCode);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Webhook alert to host {Host} timed out.", uri.Host);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Webhook alert to host {Host} failed.", uri.Host);
        }
    }
}
