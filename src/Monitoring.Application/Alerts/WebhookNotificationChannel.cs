using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Monitoring.Alerts;

public sealed class WebhookNotificationChannel : INotificationChannel
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookNotificationChannel> _logger;
    private readonly IReadOnlyList<string> _endpoints;
    private readonly IReadOnlyDictionary<string, string> _defaultHeaders;

    public WebhookNotificationChannel(
        IHttpClientFactory httpClientFactory,
        ILogger<WebhookNotificationChannel> logger,
        IReadOnlyList<string> endpoints,
        IReadOnlyDictionary<string, string> defaultHeaders)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _endpoints = endpoints;
        _defaultHeaders = defaultHeaders;
    }

    public async Task SendAsync(TargetSnapshot target, AlertPayload payload, CancellationToken cancellationToken)
    {
        if (_endpoints.Count == 0)
        {
            _logger.LogDebug("Skipping webhook notification for target {TargetId} because no endpoints were configured.", target.TargetId);
            return;
        }

        var body = BuildPayload(target, payload);

        foreach (var endpoint in _endpoints)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                continue;
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };

                foreach (var header in _defaultHeaders)
                {
                    if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value))
                    {
                        request.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }

                var client = _httpClientFactory.CreateClient("Monitoring.Webhooks");
                using var response = await client.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Webhook notification returned non-success status {Status} for target {TargetId}.",
                        (int)response.StatusCode,
                        target.TargetId);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Webhook notification failed for target {TargetId}.", target.TargetId);
            }
        }
    }

    private static string BuildPayload(TargetSnapshot target, AlertPayload payload)
    {
        var document = new
        {
            eventType = payload.EventType.ToString(),
            eventAt = payload.EventAt,
            target = new
            {
                id = target.TargetId,
                name = target.Name,
                type = target.Type.ToString(),
                endpoint = target.Endpoint,
                status = target.Status.ToString(),
                category = target.Category,
                lastCheckedAt = target.LastCheckedAt,
                lastStatusChangeAt = target.LastStatusChangeAt,
                firstDownAt = target.FirstDownAt,
                lastUpAt = target.LastUpAt
            },
            details = new
            {
                errorSummary = payload.ErrorSummary,
                responseTimeMs = payload.ResponseTimeMs,
                outage = payload.CurrentOutage == null
                    ? null
                    : new
                    {
                        id = payload.CurrentOutage.OutageId,
                        startedAt = payload.CurrentOutage.StartedAt,
                        endedAt = payload.CurrentOutage.EndedAt,
                        failureCount = payload.CurrentOutage.FailureCount,
                        totalDurationSec = payload.CurrentOutage.TotalDurationSec
                    }
            }
        };

        return JsonSerializer.Serialize(document, SerializerOptions);
    }
}
