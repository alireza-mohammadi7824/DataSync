using System;
namespace Monitoring.Targets;

public class CreateUpdateMaintenanceWindowDto
{
    public Guid? TargetId { get; set; }

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    public string? Reason { get; set; }
}
