using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Monitoring.Alerts.Delivery;

namespace Monitoring.Alerts;

public sealed class AlertDispatcher
{
    private readonly ILogger<AlertDispatcher> _logger;
    private readonly NotificationChannelResolver _resolver;

    public AlertDispatcher(ILogger<AlertDispatcher> logger, IEnumerable<INotificationChannel> channels)
    {
        _logger = logger;
        _resolver = new NotificationChannelResolver(channels ?? Array.Empty<INotificationChannel>());
    }

    public async Task DispatchAsync(AlertNotificationDto dto, CancellationToken cancellationToken)
    {
        if (dto == null)
        {
            throw new ArgumentNullException(nameof(dto));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var enabledChannels = dto.Channels?
            .Where(channel => channel.Enabled)
            .ToArray() ?? Array.Empty<NotificationChannelDto>();

        if (enabledChannels.Length == 0)
        {
            _logger.LogInformation("Alert dispatch skipped for target {TargetId}: no enabled channels.", dto.TargetId);
            return;
        }

        var distinctTypes = enabledChannels
            .Select(channel => channel.Type)
            .Distinct()
            .ToArray();

        _logger.LogInformation(
            "Dispatching alert {AlertName} for target {TargetId} to {ChannelCount} channels.",
            dto.AlertName,
            dto.TargetId,
            distinctTypes.Length);

        var tasks = new List<Task>(distinctTypes.Length);
        var dispatchedTypes = new HashSet<NotificationChannelType>();

        foreach (var channelConfig in enabledChannels)
        {
            if (!TryResolveChannel(channelConfig.Type, out var channel))
            {
                _logger.LogError(
                    "Notification channel {ChannelType} is not registered for target {TargetId}.",
                    channelConfig.Type,
                    dto.TargetId);
                continue;
            }

            if (dispatchedTypes.Add(channel.Type))
            {
                tasks.Add(SendAsync(channel, dto, cancellationToken));
            }
        }

        if (tasks.Count == 0)
        {
            return;
        }

        await Task.WhenAll(tasks);
    }

    private bool TryResolveChannel(NotificationChannelType type, out INotificationChannel channel)
    {
        try
        {
            channel = _resolver.Resolve(type);
            return true;
        }
        catch (KeyNotFoundException)
        {
            channel = null!;
            return false;
        }
    }

    private Task SendAsync(INotificationChannel channel, AlertNotificationDto dto, CancellationToken cancellationToken)
    {
        return ExecuteAsync();

        async Task ExecuteAsync()
        {
            try
            {
                await channel.SendAsync(dto, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to dispatch alert {AlertName} for target {TargetId} via {ChannelType} channel.",
                    dto.AlertName,
                    dto.TargetId,
                    channel.Type);
            }
        }
    }
}
