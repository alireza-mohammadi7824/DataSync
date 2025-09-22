using System;
using System.Collections.Generic;
using Monitoring.Alerts;

namespace Monitoring.Alerts.Delivery;

public sealed class NotificationChannelResolver
{
    private readonly IReadOnlyDictionary<NotificationChannelType, INotificationChannel> _channels;

    public NotificationChannelResolver(IEnumerable<INotificationChannel> channels)
    {
        if (channels == null)
        {
            throw new ArgumentNullException(nameof(channels));
        }

        var map = new Dictionary<NotificationChannelType, INotificationChannel>();
        foreach (var channel in channels)
        {
            if (channel == null)
            {
                continue;
            }

            if (!map.ContainsKey(channel.Type))
            {
                map[channel.Type] = channel;
            }
        }

        _channels = map;
    }

    public INotificationChannel Resolve(NotificationChannelType type)
    {
        if (!_channels.TryGetValue(type, out var channel))
        {
            throw new KeyNotFoundException($"Notification channel '{type}' is not registered.");
        }

        return channel;
    }
}
