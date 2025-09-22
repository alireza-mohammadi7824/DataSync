using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Monitoring.Alerts;
using Volo.Abp.Emailing;

namespace Monitoring.Alerts.Delivery;

public sealed class EmailNotificationChannel : INotificationChannel
{
    private readonly IEmailSender _emailSender;
    private readonly ILogger<EmailNotificationChannel> _logger;

    public EmailNotificationChannel(IEmailSender emailSender, ILogger<EmailNotificationChannel> logger)
    {
        _emailSender = emailSender;
        _logger = logger;
    }

    public NotificationChannelType Type => NotificationChannelType.Email;

    public async Task SendAsync(AlertNotificationDto dto, CancellationToken cancellationToken)
    {
        if (dto == null)
        {
            throw new ArgumentNullException(nameof(dto));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var channelEntries = dto.Channels?
            .Where(channel => channel.Enabled && channel.Type == NotificationChannelType.Email)
            .ToArray() ?? Array.Empty<NotificationChannelDto>();

        if (channelEntries.Length == 0)
        {
            _logger.LogDebug("Email notification skipped for target {TargetId}: no channel configured.", dto.TargetId);
            return;
        }

        var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in channelEntries)
        {
            if (string.IsNullOrWhiteSpace(entry.Destination))
            {
                continue;
            }

            var splits = entry.Destination.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            foreach (var recipient in splits)
            {
                if (!string.IsNullOrWhiteSpace(recipient))
                {
                    recipients.Add(recipient);
                }
            }
        }

        if (recipients.Count == 0)
        {
            _logger.LogDebug("Email notification skipped for target {TargetId}: no recipients resolved.", dto.TargetId);
            return;
        }

        var subjectBuilder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(dto.Severity))
        {
            subjectBuilder.Append(dto.Severity!.Trim());
            subjectBuilder.Append(':');
            subjectBuilder.Append(' ');
        }

        subjectBuilder.Append(dto.TargetName);
        subjectBuilder.Append(" - ");
        subjectBuilder.Append(dto.AlertName);

        var subject = subjectBuilder.ToString();

        var bodyBuilder = new StringBuilder();
        bodyBuilder.AppendLine($"Target: {dto.TargetName}");
        bodyBuilder.AppendLine($"Alert: {dto.AlertName}");
        bodyBuilder.AppendLine($"Severity: {dto.Severity}");
        bodyBuilder.AppendLine($"Triggered At (UTC): {dto.TriggeredAtUtc:O}");

        if (!string.IsNullOrWhiteSpace(dto.Message))
        {
            bodyBuilder.AppendLine();
            bodyBuilder.AppendLine(dto.Message!.Trim());
        }

        var body = bodyBuilder.ToString();
        var sent = 0;

        foreach (var recipient in recipients)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await _emailSender.SendAsync(recipient, subject, body);
                sent++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send email alert for target {TargetId} to {Recipient}.", dto.TargetId, recipient);
            }
        }

        if (sent > 0)
        {
            _logger.LogInformation("Sent email alert to {RecipientCount} recipients for target {TargetId}.", sent, dto.TargetId);
        }
    }
}
