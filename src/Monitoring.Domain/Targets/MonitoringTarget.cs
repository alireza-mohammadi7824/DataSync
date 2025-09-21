using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Monitoring.Targets;

public class MonitoringTarget : FullAuditedAggregateRoot<Guid>
{
    public string Name { get; private set; } = null!;

    public ServiceType Type { get; private set; }
    public string Endpoint { get; private set; } = null!;

    public string? SettingsJson { get; private set; }
    public int CheckIntervalSeconds { get; private set; }
    public int TimeoutSeconds { get; private set; }
    public int MaxRetryAttempts { get; private set; }
    public int RetryDelaySeconds { get; private set; }
    public string? Category { get; private set; }
    public bool IsActive { get; private set; }
    public ServiceStatus CurrentStatus { get; private set; }
    public DateTime? LastCheckedAt { get; private set; }
    public DateTime? LastStatusChangeAt { get; private set; }
    public DateTime NextDueAt { get; private set; }
    public int ConsecutiveFailures { get; private set; }
    public DateTime? FirstDownAt { get; private set; }
    public DateTime? LastUpAt { get; private set; }
    protected MonitoringTarget()
    {
    }

    public MonitoringTarget(
        Guid id,
        string name,
        ServiceType type,
        string endpoint,
        int checkIntervalSeconds,
        int timeoutSeconds,
        int maxRetryAttempts,
        int retryDelaySeconds,
        bool isActive,
        ServiceStatus currentStatus,
        DateTime nextDueAt,
        string? settingsJson = null,
        string? category = null)
        : base(id)
    {
        SetName(name);
        SetType(type);
        SetEndpoint(endpoint);
        UpdateCheckIntervalSeconds(checkIntervalSeconds);
        UpdateTimeoutSeconds(timeoutSeconds);
        UpdateRetrySettings(maxRetryAttempts, retryDelaySeconds);
        SetSettingsJson(settingsJson);
        SetCategory(category);
        UpdateActivation(isActive);
        SetCurrentStatus(currentStatus);
        SetNextDueAt(nextDueAt);
    }

    public void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), MonitoringTargetConsts.NameMaxLength);
    }

    public void SetType(ServiceType type)
    {
        if (!Enum.IsDefined(typeof(ServiceType), type))
        {
            throw new BusinessException("Monitoring:InvalidServiceType");
        }

        Type = type;
    }

    public void SetEndpoint(string endpoint)
    {
        Endpoint = Check.NotNullOrWhiteSpace(endpoint, nameof(endpoint), MonitoringTargetConsts.EndpointMaxLength);
    }

    public void SetSettingsJson(string? settingsJson)
    {
        if (!settingsJson.IsNullOrWhiteSpace())
        {
            Check.Length(settingsJson, nameof(settingsJson), MonitoringTargetConsts.SettingsJsonMaxLength, 0);
        }

        SettingsJson = settingsJson;
    }

    public void UpdateCheckIntervalSeconds(int seconds)
    {
        if (seconds <= 0)
        {
            throw new BusinessException("Monitoring:InvalidCheckInterval")
                .WithData("Minimum", 1);
        }

        CheckIntervalSeconds = seconds;
    }

    public void UpdateTimeoutSeconds(int seconds)
    {
        if (seconds <= 0)
        {
            throw new BusinessException("Monitoring:InvalidTimeout")
                .WithData("Minimum", 1);
        }

        TimeoutSeconds = seconds;
    }

    public void UpdateRetrySettings(int maxRetryAttempts, int retryDelaySeconds)
    {
        if (maxRetryAttempts < 0)
        {
            throw new BusinessException("Monitoring:InvalidRetryAttempts")
                .WithData("Minimum", 0);
        }

        if (retryDelaySeconds < 0)
        {
            throw new BusinessException("Monitoring:InvalidRetryDelay")
                .WithData("Minimum", 0);
        }

        MaxRetryAttempts = maxRetryAttempts;
        RetryDelaySeconds = retryDelaySeconds;
    }

    public void SetCategory(string? category)
    {
        if (!category.IsNullOrWhiteSpace())
        {
            Check.Length(category, nameof(category), MonitoringTargetConsts.CategoryMaxLength, 0);
        }

        Category = category;
    }

    public void UpdateActivation(bool isActive)
    {
        IsActive = isActive;
    }

    public void SetCurrentStatus(ServiceStatus status)
    {
        if (!Enum.IsDefined(typeof(ServiceStatus), status))
        {
            throw new BusinessException("Monitoring:InvalidServiceStatus");
        }

        CurrentStatus = status;
    }

    public void SetLastCheckedAt(DateTime? lastCheckedAt)
    {
        LastCheckedAt = lastCheckedAt;
    }

    public void SetLastStatusChangeAt(DateTime? lastStatusChangeAt)
    {
        LastStatusChangeAt = lastStatusChangeAt;
    }

    public void SetNextDueAt(DateTime nextDueAt)
    {
        if (nextDueAt == default)
        {
            throw new BusinessException("Monitoring:InvalidNextDueAt");
        }

        NextDueAt = nextDueAt;
    }

    public void SetConsecutiveFailures(int consecutiveFailures)
    {
        if (consecutiveFailures < 0)
        {
            throw new BusinessException("Monitoring:InvalidConsecutiveFailures")
                .WithData("Minimum", 0);
        }

        ConsecutiveFailures = consecutiveFailures;
    }

    public void SetFirstDownAt(DateTime? firstDownAt)
    {
        FirstDownAt = firstDownAt;
    }

    public void SetLastUpAt(DateTime? lastUpAt)
    {
        LastUpAt = lastUpAt;
    }
}
