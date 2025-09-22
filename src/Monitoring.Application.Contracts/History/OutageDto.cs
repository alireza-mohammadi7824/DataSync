using System;

namespace Monitoring.History;

public sealed class OutageDto
{
    public Guid Id { get; init; }

    public DateTime StartedAtUtc { get; init; }

    public DateTime? EndedAtUtc { get; init; }

    public TimeSpan? Duration { get; init; }

    public string? Reason { get; init; }
}
