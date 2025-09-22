using System;
using System.Collections.Generic;
using System.Linq;
using Monitoring.Endpoints;

namespace Monitoring.Execution;

public sealed class HealthCheckProviderResolver
{
    private readonly IReadOnlyDictionary<EndpointType, IHealthCheckProvider> _map;

    public HealthCheckProviderResolver(IEnumerable<IHealthCheckProvider> providers)
    {
        _map = providers.ToDictionary(p => p.Type);
    }

    public IHealthCheckProvider Get(EndpointType type)
    {
        if (_map.TryGetValue(type, out var provider))
        {
            return provider;
        }

        throw new InvalidOperationException($"Unknown provider: {type}");
    }
}
