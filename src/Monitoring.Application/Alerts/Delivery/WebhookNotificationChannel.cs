using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Monitoring.Alerts;

namespace Monitoring.Alerts.Delivery;

public sealed class WebhookNotificationChannel : INotificationChannel
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookNotificationChannel> _logger;

    public WebhookNotificationChannel(IHttpClientFactory httpClientFactory, ILogger<WebhookNotificationChannel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public NotificationChannelType Type => NotificationChannelType.Webhook;

    public async Task SendAsync(AlertNotificationDto dto, CancellationToken cancellationToken)
    {
        if (dto == null)
        {
            throw new ArgumentNullException(nameof(dto));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var destinations = dto.Channels?
            .Where(channel => channel.Enabled && channel.Type == NotificationChannelType.Webhook)
            .Select(channel => channel.Destination)
            .Where(destination => !string.IsNullOrWhiteSpace(destination))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<string>();

        if (destinations.Length == 0)
        {
            _logger.LogDebug("Webhook notification skipped for target {TargetId}: no destination configured.", dto.TargetId);
            return;
        }

        foreach (var destination in destinations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!Uri.TryCreate(destination, UriKind.Absolute, out var uri))
            {
                _logger.LogWarning("Webhook destination {Destination} is invalid for target {TargetId}.", destination, dto.TargetId);
                continue;
            }

            var client = _httpClientFactory.CreateClient(nameof(WebhookNotificationChannel));
            client.Timeout = RequestTimeout;

            try
            {
                using var response = await client.PostAsJsonAsync(uri, dto, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Webhook notification to {Destination} for target {TargetId} returned status {StatusCode}.",
                        Sanitize(uri),
                        dto.TargetId,
                        (int)response.StatusCode);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to send webhook notification to {Destination} for target {TargetId}.",
                    Sanitize(uri),
                    dto.TargetId);
            }
        }
    }

    private static string Sanitize(Uri uri)
    {
        if (uri == null)
        {
            return string.Empty;
        }

        return uri.GetLeftPart(UriPartial.Authority);
    }
}
