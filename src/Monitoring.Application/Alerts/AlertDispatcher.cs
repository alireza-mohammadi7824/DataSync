using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Monitoring.Targets;
using Volo.Abp.Timing;

namespace Monitoring.Alerts;

public sealed class AlertDispatcher
{
    private readonly INotificationChannelResolver _channelResolver;
    private readonly AlertThrottleStore _throttleStore;
    private readonly IClock _clock;
    private readonly ILogger<AlertDispatcher> _logger;

    public AlertDispatcher(
        INotificationChannelResolver channelResolver,
        AlertThrottleStore throttleStore,
        IClock clock,
        ILogger<AlertDispatcher> logger)
    {
        _channelResolver = channelResolver;
        _throttleStore = throttleStore;
        _clock = clock;
        _logger = logger;
    }

    public async Task<int> DispatchAsync(
        MonitoringTarget target,
        AlertEvaluationResult evaluation,
        AlertPayload payload,
        CancellationToken ct = default)
    {
        if (!evaluation.ShouldAlert || evaluation.Policies.Count == 0)
        {
            return 0;
        }

        var eventType = evaluation.EventType ?? payload.EventType;
        var nowUtc = _clock.Now;
        var snapshot = $"{target.Name} ({payload.Status})";
        var sentCount = 0;

        foreach (var policy in evaluation.Policies)
        {
            var cooldown = policy.CooldownSeconds > 0 ? policy.CooldownSeconds : 300;
            if (_throttleStore.ShouldThrottle(target.Id, eventType, cooldown, nowUtc, out var remaining))
            {
                _logger.LogInformation(
                    "Alert for target {TargetId} ({EventType}) throttled for {RemainingSeconds} seconds.",
                    target.Id,
                    eventType,
                    (int)Math.Ceiling(remaining.TotalSeconds));
                continue;
            }

            var recipients = ParseEmails(policy.Emails);
            if (recipients.Count > 0)
            {
                var body = BuildEmailBody(target, payload, evaluation.CurrentOutage);
                var emailDispatch = AlertDispatch.Create(
                    "email",
                    snapshot,
                    body,
                    recipients,
                    destination: null,
                    summary: payload.Summary);

                if (await TrySendAsync(emailDispatch, eventType, target.Id, ct))
                {
                    sentCount++;
                }
            }

            if (!string.IsNullOrWhiteSpace(policy.WebhookUrl))
            {
                var webhookPayload = BuildWebhookPayload(target, payload);
                var webhookDispatch = AlertDispatch.Create(
                    "webhook",
                    snapshot,
                    webhookPayload,
                    Array.Empty<string>(),
                    policy.WebhookUrl,
                    payload.Summary);

                if (await TrySendAsync(webhookDispatch, eventType, target.Id, ct))
                {
                    sentCount++;
                }
            }
        }

        if (sentCount > 0)
        {
            _throttleStore.MarkSent(target.Id, eventType, nowUtc);
        }

        return sentCount;
    }

    private async Task<bool> TrySendAsync(AlertDispatch dispatch, string eventType, Guid targetId, CancellationToken ct)
    {
        try
        {
            var channel = _channelResolver.Resolve(dispatch.Channel);
            var started = _clock.Now;
            await channel.SendAsync(dispatch, ct);
            var elapsed = (_clock.Now - started).TotalMilliseconds;

            _logger.LogInformation(
                "Alert dispatch via {Channel} for target {TargetId} ({EventType}) completed in {Duration}ms.",
                channel.Name,
                targetId,
                eventType,
                (int)Math.Round(elapsed));
            return true;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(
                ex,
                "Alert dispatch via {Channel} for target {TargetId} ({EventType}) failed.",
                dispatch.Channel,
                targetId,
                eventType);
            return false;
        }
    }

    private static IReadOnlyList<string> ParseEmails(string emails)
    {
        if (string.IsNullOrWhiteSpace(emails))
        {
            return Array.Empty<string>();
        }

        return emails
            .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim())
            .Where(e => e.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string BuildEmailBody(MonitoringTarget target, AlertPayload payload, OutageWindow? outage)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"<h3>{payload.Summary}</h3>");
        builder.AppendLine("<ul>");
        builder.AppendLine($"<li>Target: {target.Name} ({target.Id})</li>");
        builder.AppendLine($"<li>Status: {payload.Status}</li>");
        builder.AppendLine($"<li>Event Type: {payload.EventType}</li>");
        builder.AppendLine($"<li>Started At: {payload.StartedAt:O}</li>");

        if (payload.EndedAt.HasValue)
        {
            builder.AppendLine($"<li>Ended At: {payload.EndedAt.Value:O}</li>");
        }

        if (payload.Duration.HasValue)
        {
            builder.AppendLine($"<li>Duration: {payload.Duration.Value}</li>");
        }

        builder.AppendLine("</ul>");

        if (outage != null)
        {
            builder.AppendLine("<p>Outage details:</p>");
            builder.AppendLine("<ul>");
            builder.AppendLine($"<li>Outage Started: {outage.StartedAt:O}</li>");
            if (outage.EndedAt.HasValue)
            {
                builder.AppendLine($"<li>Outage Ended: {outage.EndedAt.Value:O}</li>");
            }
            builder.AppendLine("</ul>");
        }

        return builder.ToString();
    }

    private static string BuildWebhookPayload(MonitoringTarget target, AlertPayload payload)
    {
        var body = new
        {
            targetId = payload.TargetId,
            name = target.Name,
            status = payload.Status,
            startedAt = payload.StartedAt,
            endedAt = payload.EndedAt,
            duration = payload.Duration?.TotalSeconds,
            eventType = payload.EventType,
            summary = payload.Summary
        };

        return JsonSerializer.Serialize(body);
    }
}
