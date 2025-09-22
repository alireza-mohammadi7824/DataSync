using System;
using System.Threading;
using System.Threading.Tasks;
using Monitoring.Targets;
using Volo.Abp.Domain.Repositories;

namespace Monitoring.Execution;

internal static class MaintenanceWindowHelper
{
    public static async Task<MaintenanceState> GetStateAsync(
        IReadOnlyRepository<MaintenanceWindow, Guid> repository,
        Guid targetId,
        DateTime timestamp,
        CancellationToken cancellationToken)
    {
        var hasActive = await repository.AnyAsync(
            window => window.StartUtc <= timestamp && window.EndUtc >= timestamp &&
                      (window.TargetId == null || window.TargetId == targetId),
            cancellationToken);

        if (!hasActive)
        {
            return MaintenanceState.None;
        }

        var hasRecordOnly = await repository.AnyAsync(
            window => window.StartUtc <= timestamp && window.EndUtc >= timestamp &&
                      (window.TargetId == null || window.TargetId == targetId) &&
                      window.RecordButDontAlert,
            cancellationToken);

        return new MaintenanceState(true, hasRecordOnly);
    }
}

internal readonly record struct MaintenanceState(bool HasActive, bool RecordButDontAlert)
{
    public bool ShouldSkip => HasActive && !RecordButDontAlert;

    public static MaintenanceState None { get; } = new(false, false);
}
