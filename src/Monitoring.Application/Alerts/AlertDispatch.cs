using System;
using Monitoring.Targets;

namespace Monitoring.Alerts;

public sealed record AlertDispatch(
    string Channel,
    string TargetSnapshot,
    string Payload)
{
    public TargetSnapshot? SnapshotModel { get; init; }

    public AlertPayload? PayloadModel { get; init; }

    public INotificationChannel? ChannelInstance { get; init; }

    public static AlertDispatch Create(string channel, string targetSnapshot, string payload)
        => new(channel, targetSnapshot, payload);

    public static AlertDispatch Create(
        string channel,
        string targetSnapshot,
        string payload,
        TargetSnapshot snapshotModel,
        AlertPayload payloadModel,
        INotificationChannel channelInstance)
    {
        if (snapshotModel == null)
        {
            throw new ArgumentNullException(nameof(snapshotModel));
        }

        if (payloadModel == null)
        {
            throw new ArgumentNullException(nameof(payloadModel));
        }

        if (channelInstance == null)
        {
            throw new ArgumentNullException(nameof(channelInstance));
        }

        return new AlertDispatch(channel, targetSnapshot, payload)
        {
            SnapshotModel = snapshotModel,
            PayloadModel = payloadModel,
            ChannelInstance = channelInstance
        };
    }
}
