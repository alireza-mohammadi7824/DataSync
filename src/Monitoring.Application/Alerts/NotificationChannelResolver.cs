using System;
using System.Collections.Generic;
using System.Linq;

namespace Monitoring.Alerts;

public sealed class NotificationChannelResolver : INotificationChannelResolver
{
    private readonly IReadOnlyDictionary<string, INotificationChannel> _channels;

    public NotificationChannelResolver(IEnumerable<INotificationChannel> channels)
    {
        _channels = channels.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
    }

    public INotificationChannel Resolve(string channel)
    {
        if (string.IsNullOrWhiteSpace(channel))
        {
            throw new ArgumentException("Channel name is required.", nameof(channel));
        }

        if (_channels.TryGetValue(channel, out var resolved))
        {
            return resolved;
        }

        throw new InvalidOperationException($"Unknown notification channel: {channel}");
    }
}
