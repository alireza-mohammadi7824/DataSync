using System;

namespace Monitoring.History;

public sealed class TimelineRequestDto
{
    public DateTime FromUtc { get; init; }

    public DateTime ToUtc { get; init; }
}
