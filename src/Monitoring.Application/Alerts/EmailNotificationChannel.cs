using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monitoring.Options;
using Volo.Abp.Emailing;

namespace Monitoring.Alerts;

public sealed class EmailNotificationChannel : INotificationChannel
{
    private readonly IEmailSender _emailSender;
    private readonly ILogger<EmailNotificationChannel> _logger;
    private readonly string _subjectPrefix;

    public EmailNotificationChannel(
        IEmailSender emailSender,
        ILogger<EmailNotificationChannel> logger,
        IOptions<MonitoringOptions> options)
    {
        _emailSender = emailSender;
        _logger = logger;
        _subjectPrefix = options.Value.Notifications.Email.SubjectPrefix ?? "[Monitoring]";
    }

    public string Name => "email";

    public async Task SendAsync(AlertDispatch dispatch, CancellationToken ct = default)
    {
        if (dispatch.Recipients.Count == 0)
        {
            _logger.LogDebug("Skipping email alert because no recipients were provided.");
            return;
        }

        var subject = BuildSubject(dispatch.Summary);
        var body = dispatch.Payload;

        foreach (var recipient in dispatch.Recipients)
        {
            if (string.IsNullOrWhiteSpace(recipient))
            {
                continue;
            }

            try
            {
                await _emailSender.SendAsync(recipient, subject, body, isBodyHtml: true);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Failed to send alert email to configured recipient.");
            }
        }
    }

    private string BuildSubject(string summary)
    {
        var prefix = _subjectPrefix?.Trim();
        if (string.IsNullOrEmpty(prefix))
        {
            return summary;
        }

        var builder = new StringBuilder();
        builder.Append(prefix);
        builder.Append(' ');
        builder.Append(summary);
        return builder.ToString();
    }
}
