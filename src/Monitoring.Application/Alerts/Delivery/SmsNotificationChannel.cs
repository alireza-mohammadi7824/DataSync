using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Monitoring.Alerts;

namespace Monitoring.Alerts.Delivery;

public sealed class SmsNotificationChannel : INotificationChannel
{
    private readonly ILogger<SmsNotificationChannel> _logger;

    public SmsNotificationChannel(ILogger<SmsNotificationChannel> logger)
    {
        _logger = logger;
    }

    public NotificationChannelType Type => NotificationChannelType.Sms;

    public Task SendAsync(AlertNotificationDto dto, CancellationToken cancellationToken)
    {
        if (dto == null)
        {
            throw new ArgumentNullException(nameof(dto));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var destinations = dto.Channels?
            .Where(channel => channel.Enabled && channel.Type == NotificationChannelType.Sms)
            .Select(channel => channel.Destination)
            .Where(destination => !string.IsNullOrWhiteSpace(destination))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<string>();

        if (destinations.Length == 0)
        {
            _logger.LogDebug("SMS notification skipped for target {TargetId}: no destination configured.", dto.TargetId);
            return Task.CompletedTask;
        }

        foreach (var destination in destinations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogWarning("SMS channel not implemented. Would send to {Destination} for target {TargetId}.", destination, dto.TargetId);
        }

        return Task.CompletedTask;
    }
}
