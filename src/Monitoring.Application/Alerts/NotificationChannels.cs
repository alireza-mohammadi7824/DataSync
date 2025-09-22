using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Monitoring.Alerts;

public interface INotificationChannel
{
    Task SendAsync(TargetSnapshot target, AlertPayload payload, CancellationToken cancellationToken);
}

public interface INotificationChannelResolver
{
    IReadOnlyList<NotificationChannelDescriptor> ResolveChannels(AlertChannelConfiguration configuration);

    INotificationChannel Resolve(string channel);
}

public sealed record NotificationChannelDescriptor(string Name, INotificationChannel Channel);

public sealed class AlertChannelConfiguration
{
    public AlertChannelConfiguration(Dictionary<string, string[]> channels)
    {
        Channels = channels == null
            ? new Dictionary<string, string[]>(System.StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string[]>(channels, System.StringComparer.OrdinalIgnoreCase);
    }

    public Dictionary<string, string[]> Channels { get; }
}
