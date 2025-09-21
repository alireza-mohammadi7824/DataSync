using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Monitoring.Alerts;

public sealed class TelegramNotificationChannel : INotificationChannel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TelegramNotificationChannel> _logger;
    private readonly string _botToken;
    private readonly IReadOnlyList<string> _chatIds;

    public TelegramNotificationChannel(
        IHttpClientFactory httpClientFactory,
        ILogger<TelegramNotificationChannel> logger,
        string botToken,
        IReadOnlyList<string> chatIds)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _botToken = botToken;
        _chatIds = chatIds;
    }

    public async Task SendAsync(TargetSnapshot target, AlertPayload payload, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_botToken) || _chatIds.Count == 0)
        {
            _logger.LogDebug("Skipping Telegram notification for target {TargetId} due to missing configuration.", target.TargetId);
            return;
        }

        var message = BuildMessage(target, payload);
        var client = _httpClientFactory.CreateClient("Monitoring.Telegram");

        foreach (var chatId in _chatIds)
        {
            if (string.IsNullOrWhiteSpace(chatId))
            {
                continue;
            }

            try
            {
                var requestUri = $"https://api.telegram.org/bot{_botToken}/sendMessage";
                using var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["chat_id"] = chatId,
                    ["text"] = message,
                    ["parse_mode"] = "MarkdownV2"
                });

                using var response = await client.PostAsync(requestUri, content, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Telegram notification returned non-success status {Status} for target {TargetId}.",
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
                _logger.LogWarning(ex, "Telegram notification failed for target {TargetId}.", target.TargetId);
            }
        }
    }

    private static string BuildMessage(TargetSnapshot target, AlertPayload payload)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"*{EscapeMarkdown(target.Name)}* - {payload.EventType}");
        builder.AppendLine($"Endpoint: `{EscapeMarkdown(target.Endpoint)}`");
        builder.AppendLine($"Status: {payload.EventType} at {payload.EventAt:O}");

        if (!string.IsNullOrWhiteSpace(payload.ErrorSummary))
        {
            builder.AppendLine($"Error: {EscapeMarkdown(payload.ErrorSummary!)}");
        }

        if (payload.ResponseTimeMs.HasValue)
        {
            builder.AppendLine($"Response: {payload.ResponseTimeMs.Value} ms");
        }

        if (payload.CurrentOutage != null)
        {
            builder.AppendLine($"Outage started: {payload.CurrentOutage.StartedAt:O}");
            if (payload.CurrentOutage.EndedAt.HasValue)
            {
                builder.AppendLine($"Outage ended: {payload.CurrentOutage.EndedAt.Value:O}");
            }
        }

        return builder.ToString();
    }

    private static string EscapeMarkdown(string value)
    {
        var builder = new StringBuilder(value.Length * 2);
        foreach (var ch in value)
        {
            if ("_[]()~`>#+-=|{}.!".IndexOf(ch) >= 0)
            {
                builder.Append('\\');
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }
}
