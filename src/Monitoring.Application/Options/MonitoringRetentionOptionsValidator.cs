using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Monitoring.Options;

public sealed class MonitoringRetentionOptionsValidator : IValidateOptions<MonitoringRetentionOptions>
{
    private static readonly Regex SchedulePattern = new("^\\d{2}:\\d{2}$", RegexOptions.Compiled);

    public ValidateOptionsResult Validate(string? name, MonitoringRetentionOptions options)
    {
        if (options.HistoryDays < 1 || options.HistoryDays > 3_650)
        {
            return ValidateOptionsResult.Fail("HistoryDays must be 1..3650");
        }

        if (options.MaxHistoryPerTarget < 1_000 || options.MaxHistoryPerTarget > 1_000_000)
        {
            return ValidateOptionsResult.Fail("MaxHistoryPerTarget must be 1000..1000000");
        }

        if (options.PurgeBatchSize < 100 || options.PurgeBatchSize > 10_000)
        {
            return ValidateOptionsResult.Fail("PurgeBatchSize must be 100..10000");
        }

        if (options.KeepLastOutagesPerTarget < 0 || options.KeepLastOutagesPerTarget > 10_000)
        {
            return ValidateOptionsResult.Fail("KeepLastOutagesPerTarget must be 0..10000");
        }

        if (!string.IsNullOrWhiteSpace(options.ScheduleUtc) && !SchedulePattern.IsMatch(options.ScheduleUtc))
        {
            return ValidateOptionsResult.Fail("ScheduleUtc must be HH:mm");
        }

        return ValidateOptionsResult.Success;
    }
}
