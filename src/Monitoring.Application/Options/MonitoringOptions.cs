using System;
using System.Collections.Generic;

namespace Monitoring.Options;

public class MonitoringOptions
{
    public AlertDefaultsOptions AlertDefaults { get; set; } = new();

    public NotificationChannelOptions Notifications { get; set; } = new();

    public DashboardOptions Dashboard { get; set; } = new();

    public ExecutionOptions Execution { get; set; } = new();

    public RetentionOptions Retention { get; set; } = new();

    public class AlertDefaultsOptions
    {
        public bool Enabled { get; set; } = true;

        public int NotifyAfterFailures { get; set; } = 1;

        public int RepeatMinutes { get; set; } = 60;

        public int RecoverQuietMinutes { get; set; } = 10;

        public bool SuppressDuringMaintenance { get; set; } = true;

        public Dictionary<string, string[]>? DefaultChannels { get; set; }
            = new();
    }

    public class NotificationChannelOptions
    {
        public EmailChannelOptions Email { get; set; } = new();
        public TelegramChannelOptions Telegram { get; set; } = new();
        public WebhookChannelOptions Webhook { get; set; } = new();
    }

    public class EmailChannelOptions
    {
        public string? FromAddress { get; set; }
            = null;

        public string SubjectPrefix { get; set; } = "[Monitoring]";
    }

    public class TelegramChannelOptions
    {
        public string? BotToken { get; set; }
            = null;

        public string[] ChatIds { get; set; } = Array.Empty<string>();
    }

    public class WebhookChannelOptions
    {
        public Dictionary<string, string>? DefaultHeaders { get; set; }
            = new();
    }

    public class DashboardOptions
    {
        public int DefaultRangeDays { get; set; } = 7;

        public int MaxRangeDays { get; set; } = 180;
    }

    public class ExecutionOptions
    {
        public int MaxConcurrentChecks { get; set; } = 8;

        public int LockTtlBufferSeconds { get; set; } = 5;

        public int? GlobalCheckTimeoutSeconds { get; set; }
            = null;
    }

    public class RetentionOptions
    {
        public int HistoryDays { get; set; } = 90;

        public int MaxHistoryPerTarget { get; set; } = 10_000;

        public int PurgeBatchSize { get; set; } = 1_000;

        public int MinOutagesPerTarget { get; set; } = 50;
    }
}
