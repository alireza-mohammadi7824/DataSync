using System.Collections.Generic;
using Monitoring.Targets;

namespace Monitoring.Dashboard;

public class MttrMtbfDto
{
    public double? MeanTimeToRecoverSeconds { get; set; }

    public double? MeanTimeBetweenFailuresSeconds { get; set; }

    public List<MttrMtbfBreakdownDto> Breakdown { get; set; } = new();
}

public class MttrMtbfBreakdownDto
{
    public ServiceType ServiceType { get; set; }

    public double? MeanTimeToRecoverSeconds { get; set; }

    public double? MeanTimeBetweenFailuresSeconds { get; set; }
}
