using System;

namespace Monitoring.Targets;

public class HealthCheckResultDto
{
    public Guid TargetId { get; set; }

    public ServiceStatus Status { get; set; }

    public int? ResponseTimeMs { get; set; }

    public string? ErrorSummary { get; set; }

    public DateTime CheckedAt { get; set; }
}
