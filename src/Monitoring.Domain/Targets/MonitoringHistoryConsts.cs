using Volo.Abp;

namespace Monitoring.Targets;

public static class MonitoringHistoryConsts
{
    public const int TriggerSourceMaxLength = 32;
    public const int ErrorSummaryMaxLength = 512;

    public static void ValidateTriggerSource(string triggerSource)
    {
        Check.NotNullOrWhiteSpace(triggerSource, nameof(triggerSource), TriggerSourceMaxLength);
    }

    public static void ValidateErrorSummary(string? errorSummary)
    {
        if (!string.IsNullOrWhiteSpace(errorSummary))
        {
            Check.Length(errorSummary, nameof(errorSummary), ErrorSummaryMaxLength, 0);
        }
    }
}
