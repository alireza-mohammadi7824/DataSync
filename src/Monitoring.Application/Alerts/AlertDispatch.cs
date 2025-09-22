using System;
using System.Collections.Generic;
using Monitoring.Targets;
using Volo.Abp;

namespace Monitoring.Alerts;

public sealed record AlertDispatch(
    string Channel,
    TargetSnapshot TargetSnapshot,
    AlertPayload Payload,
    INotificationChannel NotificationChannel)
{
    public static List<AlertDispatch> Create(
        TargetSnapshot targetSnapshot,
        AlertPayload payload,
        IReadOnlyList<NotificationChannelDescriptor> descriptors)
    {
        if (targetSnapshot == null)
        {
            throw new ArgumentNullException(nameof(targetSnapshot));
        }

        if (payload == null)
        {
            throw new ArgumentNullException(nameof(payload));
        }

        var dispatches = new List<AlertDispatch>();
        if (descriptors == null || descriptors.Count == 0)
        {
            return dispatches;
        }

        foreach (var descriptor in descriptors)
        {
            if (descriptor == null)
            {
                continue;
            }

            var name = descriptor.Name?.Trim();
            if (name.IsNullOrWhiteSpace() || descriptor.Channel == null)
            {
                continue;
            }

            dispatches.Add(new AlertDispatch(name!, targetSnapshot, payload, descriptor.Channel));
        }

        return dispatches;
    }
}
