using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Monitoring.Options;
using Monitoring.Permissions;
using Volo.Abp.Application.Services;

namespace Monitoring.Config;

[Authorize(MonitoringPermissions.Config.View)]
public sealed class MonitoringConfigAppService : ApplicationService, IMonitoringConfigAppService
{
    private readonly IOptionsMonitor<MonitoringExecutionOptions> _executionOptions;
    private readonly IOptionsMonitor<MonitoringRetentionOptions> _retentionOptions;
    private readonly IOptionsMonitor<MonitoringAlertsOptions> _alertsOptions;

    public MonitoringConfigAppService(
        IOptionsMonitor<MonitoringExecutionOptions> executionOptions,
        IOptionsMonitor<MonitoringRetentionOptions> retentionOptions,
        IOptionsMonitor<MonitoringAlertsOptions> alertsOptions)
    {
        _executionOptions = executionOptions;
        _retentionOptions = retentionOptions;
        _alertsOptions = alertsOptions;
    }

    public Task<MonitoringConfigDto> GetAsync()
    {
        var execution = _executionOptions.CurrentValue;
        var retention = _retentionOptions.CurrentValue;
        var alerts = _alertsOptions.CurrentValue;

        var dto = new MonitoringConfigDto(
            new MonitoringExecutionConfigDto
            {
                MaxConcurrentChecks = execution.MaxConcurrentChecks,
                LockTtlBufferSeconds = execution.LockTtlBufferSeconds,
                GlobalCheckTimeoutSeconds = execution.GlobalCheckTimeoutSeconds,
                MaxRetryAttempts = execution.MaxRetryAttempts,
                MaxBackoffSeconds = execution.MaxBackoffSeconds
            },
            new MonitoringRetentionConfigDto
            {
                HistoryDays = retention.HistoryDays,
                MaxHistoryPerTarget = retention.MaxHistoryPerTarget,
                PurgeBatchSize = retention.PurgeBatchSize,
                KeepLastOutagesPerTarget = retention.KeepLastOutagesPerTarget,
                ScheduleUtc = retention.ScheduleUtc
            },
            new MonitoringAlertsConfigDto
            {
                DefaultCooldownSeconds = alerts.DefaultCooldownSeconds
            });

        return Task.FromResult(dto);
    }
}
