using System.Collections.Generic;
using Microsoft.Extensions.Options;

namespace Monitoring.Options;

public class MonitoringOptionsValidator : IValidateOptions<MonitoringOptions>
{
    public ValidateOptionsResult Validate(string? name, MonitoringOptions options)
    {
        var failures = new List<string>();

        if (options.Execution.MaxConcurrentChecks <= 0)
        {
            failures.Add("Execution.MaxConcurrentChecks must be greater than zero.");
        }

        if (options.Execution.LockTtlBufferSeconds < 0)
        {
            failures.Add("Execution.LockTtlBufferSeconds cannot be negative.");
        }

        if (options.Execution.GlobalCheckTimeoutSeconds is { } global && global <= 0)
        {
            failures.Add("Execution.GlobalCheckTimeoutSeconds must be greater than zero when specified.");
        }

        if (options.Retention.HistoryDays < 0)
        {
            failures.Add("Retention.HistoryDays cannot be negative.");
        }

        if (options.Retention.MaxHistoryPerTarget < 0)
        {
            failures.Add("Retention.MaxHistoryPerTarget cannot be negative.");
        }

        if (options.Retention.PurgeBatchSize <= 0)
        {
            failures.Add("Retention.PurgeBatchSize must be greater than zero.");
        }

        if (options.Retention.MinOutagesPerTarget < 0)
        {
            failures.Add("Retention.MinOutagesPerTarget cannot be negative.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
