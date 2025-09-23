using Microsoft.Extensions.Options;

namespace Monitoring.Options;

public sealed class MonitoringAlertsOptionsValidator : IValidateOptions<MonitoringAlertsOptions>
{
    public ValidateOptionsResult Validate(string? name, MonitoringAlertsOptions options)
    {
        if (options.DefaultCooldownSeconds < 0 || options.DefaultCooldownSeconds > 86_400)
        {
            return ValidateOptionsResult.Fail("DefaultCooldownSeconds must be 0..86400");
        }

        return ValidateOptionsResult.Success;
    }
}
