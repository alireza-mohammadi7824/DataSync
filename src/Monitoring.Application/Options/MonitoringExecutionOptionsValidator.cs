using Microsoft.Extensions.Options;

namespace Monitoring.Options;

public sealed class MonitoringExecutionOptionsValidator : IValidateOptions<MonitoringExecutionOptions>
{
    public ValidateOptionsResult Validate(string? name, MonitoringExecutionOptions options)
    {
        if (options.MaxConcurrentChecks < 1 || options.MaxConcurrentChecks > 256)
        {
            return ValidateOptionsResult.Fail("MaxConcurrentChecks must be 1..256");
        }

        if (options.LockTtlBufferSeconds < 0 || options.LockTtlBufferSeconds > 120)
        {
            return ValidateOptionsResult.Fail("LockTtlBufferSeconds must be 0..120");
        }

        if (options.GlobalCheckTimeoutSeconds is < 1 or > 600)
        {
            return ValidateOptionsResult.Fail("GlobalCheckTimeoutSeconds must be 1..600 when specified");
        }

        if (options.MaxRetryAttempts < 0 || options.MaxRetryAttempts > 10)
        {
            return ValidateOptionsResult.Fail("MaxRetryAttempts must be 0..10");
        }

        if (options.MaxBackoffSeconds < 1 || options.MaxBackoffSeconds > 300)
        {
            return ValidateOptionsResult.Fail("MaxBackoffSeconds must be 1..300");
        }

        return ValidateOptionsResult.Success;
    }
}
