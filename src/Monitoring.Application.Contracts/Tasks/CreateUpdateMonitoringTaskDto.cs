using System.ComponentModel.DataAnnotations;

namespace Monitoring.Tasks;

public class CreateUpdateMonitoringTaskDto
{
    [Required]
    [StringLength(128)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(256)]
    [Url]
    public string TargetUrl { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int CheckIntervalInSeconds { get; set; } = 300;

    public bool IsActive { get; set; } = true;

    [StringLength(128)]
    public string? AuthenticationSecretRef { get; set; }
}
