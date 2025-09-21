using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Emailing;

namespace Monitoring.Alerts;

public sealed class EmailNotificationChannel : INotificationChannel
{
    private readonly IEmailSender _emailSender;
    private readonly ILogger<EmailNotificationChannel> _logger;
    private readonly IReadOnlyList<string> _recipients;
    private readonly string? _fromAddress;
    private readonly string _subjectPrefix;

    public EmailNotificationChannel(
        IEmailSender emailSender,
        ILogger<EmailNotificationChannel> logger,
        IReadOnlyList<string> recipients,
        string? fromAddress,
        string subjectPrefix)
    {
        _emailSender = emailSender;
        _logger = logger;
        _recipients = recipients;
        _fromAddress = fromAddress;
        _subjectPrefix = subjectPrefix ?? string.Empty;
    }

    public async Task SendAsync(TargetSnapshot target, AlertPayload payload, CancellationToken cancellationToken)
    {
        if (_recipients.Count == 0)
        {
            _logger.LogDebug("Skipping email notification for target {TargetId} because no recipients were configured.", target.TargetId);
            return;
        }

        var subject = BuildSubject(payload.EventType, target.Name);
        var body = BuildBody(target, payload);

        foreach (var recipient in _recipients)
        {
            if (recipient.IsNullOrWhiteSpace())
            {
                continue;
            }

            try
            {
                if (_fromAddress.IsNullOrWhiteSpace())
                {
                    await _emailSender.SendAsync(recipient, subject, body);
                }
                else
                {
                    await _emailSender.SendAsync(new EmailMessage(
                        fromAddress: _fromAddress!,
                        to: recipient,
                        subject: subject,
                        body: body));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send email notification for target {TargetId} to configured recipient.", target.TargetId);
            }
        }
    }

    private string BuildSubject(AlertEventType eventType, string targetName)
    {
        var normalizedPrefix = string.IsNullOrWhiteSpace(_subjectPrefix) ? string.Empty : _subjectPrefix.Trim();
        var separator = normalizedPrefix.Length > 0 ? " " : string.Empty;
        return string.Concat(normalizedPrefix, separator, eventType.ToString(), " alert for ", targetName);
    }

    private static string BuildBody(TargetSnapshot target, AlertPayload payload)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Alert Type: {payload.EventType}");
        builder.AppendLine($"Service: {target.Name} ({target.Type})");
        builder.AppendLine($"Endpoint: {target.Endpoint}");
        builder.AppendLine($"Occurred At: {payload.EventAt:O}");

        if (payload.ResponseTimeMs.HasValue)
        {
            builder.AppendLine($"Response Time: {payload.ResponseTimeMs.Value} ms");
        }

        if (!payload.ErrorSummary.IsNullOrWhiteSpace())
        {
            builder.AppendLine($"Error: {payload.ErrorSummary}");
        }

        if (payload.CurrentOutage != null)
        {
            builder.AppendLine();
            builder.AppendLine("Outage Summary:");
            builder.AppendLine($"  Started: {payload.CurrentOutage.StartedAt:O}");
            if (payload.CurrentOutage.EndedAt.HasValue)
            {
                builder.AppendLine($"  Ended: {payload.CurrentOutage.EndedAt.Value:O}");
            }
            builder.AppendLine($"  Failures: {payload.CurrentOutage.FailureCount}");
            if (payload.CurrentOutage.TotalDurationSec.HasValue)
            {
                builder.AppendLine($"  Duration: {TimeSpan.FromSeconds(Math.Max(payload.CurrentOutage.TotalDurationSec.Value, 0))}");
            }
        }

        return builder.ToString();
    }
}
