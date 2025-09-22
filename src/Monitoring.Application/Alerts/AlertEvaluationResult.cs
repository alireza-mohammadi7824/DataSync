using System.Collections.Generic;
using Monitoring.Targets;

namespace Monitoring.Alerts;

public sealed record AlertEvaluationResult(
    bool ShouldAlert,
    OutageWindow? CurrentOutage)
{
    public string? EventType { get; init; }

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<AlertPolicy> Policies { get; init; } = new List<AlertPolicy>();
}
