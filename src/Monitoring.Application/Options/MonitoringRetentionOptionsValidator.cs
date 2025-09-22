using Microsoft.Extensions.Options;

namespace Monitoring.Options;

public sealed class MonitoringRetentionOptionsValidator : IValidateOptions<MonitoringRetentionOptions>
{
    public ValidateOptionsResult Validate(string? name, MonitoringRetentionOptions options)
    {
        if (options.HistoryDays < 1 || options.HistoryDays > 3650)
        {
            return ValidateOptionsResult.Fail("HistoryDays out of range.");
        }

        if (options.MaxHistoryPerTarget < 100)
        {
            return ValidateOptionsResult.Fail("MaxHistoryPerTarget too small.");
        }

        if (options.PurgeBatchSize < 100 || options.PurgeBatchSize > 100_000)
        {
            return ValidateOptionsResult.Fail("PurgeBatchSize out of range.");
        }

        if (options.KeepOutagesPerTarget < 0 || options.KeepOutagesPerTarget > 10_000)
        {
            return ValidateOptionsResult.Fail("KeepOutagesPerTarget out of range.");
        }

        return ValidateOptionsResult.Success;
    }
}
