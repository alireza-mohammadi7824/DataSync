using System.Collections.Generic;

namespace Monitoring.Options;

public class MonitoringOptions
{
    public AlertDefaultsOptions AlertDefaults { get; set; } = new();

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
}
