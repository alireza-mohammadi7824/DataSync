using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Monitoring.Tasks;

public class MonitoringTask : FullAuditedAggregateRoot<Guid>
{
    public string Name { get; private set; }

    public string TargetUrl { get; private set; }

    public bool IsActive { get; private set; }

    public int CheckIntervalInSeconds { get; private set; }

    public DateTime? LastExecutionTime { get; private set; }

    public string? AuthenticationSecretRef { get; private set; }

    public int ConsecutiveFailureCount { get; private set; }

    protected MonitoringTask()
    {
    }

    public MonitoringTask(
        Guid id,
        string name,
        string targetUrl,
        bool isActive,
        int checkIntervalInSeconds,
        string? authenticationSecretRef = null)
        : base(id)
    {
        SetName(name);
        SetTargetUrl(targetUrl);
        UpdateActivation(isActive);
        UpdateCheckInterval(checkIntervalInSeconds);
        SetAuthenticationSecretRef(authenticationSecretRef);
    }

    public void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), MonitoringTaskConsts.NameMaxLength);
    }

    public void SetTargetUrl(string targetUrl)
    {
        TargetUrl = Check.NotNullOrWhiteSpace(targetUrl, nameof(targetUrl), MonitoringTaskConsts.TargetUrlMaxLength);
    }

    public void UpdateActivation(bool isActive)
    {
        IsActive = isActive;
    }

    public void UpdateCheckInterval(int checkIntervalInSeconds)
    {
        if (checkIntervalInSeconds <= 0)
        {
            throw new ArgumentException("Check interval must be positive", nameof(checkIntervalInSeconds));
        }

        CheckIntervalInSeconds = checkIntervalInSeconds;
    }

    public void SetAuthenticationSecretRef(string? secretRef)
    {
        if (!secretRef.IsNullOrWhiteSpace() && secretRef.Length > MonitoringTaskConsts.SecretReferenceMaxLength)
        {
            throw new BusinessException("Monitoring:SecretReferenceTooLong")
                .WithData("Length", MonitoringTaskConsts.SecretReferenceMaxLength);
        }

        AuthenticationSecretRef = secretRef;
    }

    public void ReportExecution(DateTime executionTime, bool succeeded)
    {
        LastExecutionTime = executionTime;

        ConsecutiveFailureCount = succeeded
            ? 0
            : ConsecutiveFailureCount + 1;
    }
}
