using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Emailing;
using Monitoring.Options;

namespace Monitoring.Alerts;

public sealed class NotificationChannelResolver : INotificationChannelResolver
{
    private static readonly JsonSerializerOptions ChannelsSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IEmailSender _emailSender;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly MonitoringOptions _options;

    public NotificationChannelResolver(
        IEmailSender emailSender,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        IOptions<MonitoringOptions> options)
    {
        _emailSender = emailSender;
        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory;
        _options = options.Value;
    }

    public IReadOnlyList<NotificationChannelDescriptor> ResolveChannels(AlertChannelConfiguration configuration)
    {
        var channels = new List<NotificationChannelDescriptor>();
        if (configuration?.Channels == null || configuration.Channels.Count == 0)
        {
            return channels;
        }

        foreach (var kvp in configuration.Channels)
        {
            var key = kvp.Key?.Trim();
            if (key.IsNullOrWhiteSpace())
            {
                continue;
            }

            var entries = (kvp.Value ?? Array.Empty<string>()).Where(x => !x.IsNullOrWhiteSpace())
                .Select(x => x.Trim())
                .Where(x => !x.IsNullOrWhiteSpace())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (entries.Length == 0)
            {
                continue;
            }

            switch (key.ToLowerInvariant())
            {
                case "email":
                    channels.Add(CreateEmailChannel(entries));
                    break;
                case "webhook":
                    channels.Add(CreateWebhookChannel(entries));
                    break;
                case "telegram":
                    channels.Add(CreateTelegramChannel(entries));
                    break;
            }
        }

        return channels;
    }

    public INotificationChannel Resolve(string channel)
    {
        if (channel.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException("Channel name must be provided.");
        }

        var normalized = channel.Trim().ToLowerInvariant();
        var defaults = _options.AlertDefaults.DefaultChannels ?? new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        defaults.TryGetValue(normalized, out var values);
        var entries = values ?? Array.Empty<string>();

        return normalized switch
        {
            "email" => CreateEmailChannel(entries),
            "webhook" => CreateWebhookChannel(entries),
            "telegram" => CreateTelegramChannel(entries),
            _ => throw new InvalidOperationException($"Notification channel '{channel}' is not registered.")
        };
    }

    public static Dictionary<string, string[]> ParseChannelsJson(string? channelsJson)
    {
        if (channelsJson.IsNullOrWhiteSpace())
        {
            return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var dictionary = JsonSerializer.Deserialize<Dictionary<string, string[]>>(channelsJson!, ChannelsSerializerOptions);
            return dictionary == null
                ? new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string[]>(dictionary, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private NotificationChannelDescriptor CreateEmailChannel(IReadOnlyList<string> recipients)
    {
        var logger = _loggerFactory.CreateLogger<EmailNotificationChannel>();
        var channel = new EmailNotificationChannel(
            _emailSender,
            logger,
            recipients,
            _options.Notifications.Email.SubjectPrefix ?? string.Empty);

        return new NotificationChannelDescriptor("email", channel);
    }

    private NotificationChannelDescriptor CreateWebhookChannel(IReadOnlyList<string> endpoints)
    {
        var logger = _loggerFactory.CreateLogger<WebhookNotificationChannel>();
        var headers = _options.Notifications.Webhook.DefaultHeaders ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var channel = new WebhookNotificationChannel(
            _httpClientFactory,
            logger,
            endpoints,
            headers);

        return new NotificationChannelDescriptor("webhook", channel);
    }

    private NotificationChannelDescriptor CreateTelegramChannel(IReadOnlyList<string> chatIds)
    {
        var logger = _loggerFactory.CreateLogger<TelegramNotificationChannel>();
        var effectiveChatIds = chatIds.Count > 0
            ? chatIds
            : _options.Notifications.Telegram.ChatIds ?? Array.Empty<string>();

        var channel = new TelegramNotificationChannel(
            _httpClientFactory,
            logger,
            _options.Notifications.Telegram.BotToken ?? string.Empty,
            effectiveChatIds);

        return new NotificationChannelDescriptor("telegram", channel);
    }
}
