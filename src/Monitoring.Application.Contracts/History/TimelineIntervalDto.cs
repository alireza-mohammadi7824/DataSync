using System;

namespace Monitoring.History;

public sealed class TimelineIntervalDto
{
    public DateTime StartUtc { get; init; }

    public DateTime EndUtc { get; init; }

    public TargetStatusDto Status { get; init; }
}
