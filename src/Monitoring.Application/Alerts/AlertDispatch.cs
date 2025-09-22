using System.Collections.Generic;

namespace Monitoring.Alerts;

internal sealed class AlertDispatch
{
    public AlertDispatch(
        TargetSnapshot target,
        AlertPayload payload,
        IReadOnlyList<NotificationChannelDescriptor> channels)
    {
        Target = target;
        Payload = payload;
        Channels = channels;
    }

    public TargetSnapshot Target { get; }
    public AlertPayload Payload { get; }
    public IReadOnlyList<NotificationChannelDescriptor> Channels { get; }
}
