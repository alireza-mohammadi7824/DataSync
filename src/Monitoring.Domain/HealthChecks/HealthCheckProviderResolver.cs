using System;
using Microsoft.Extensions.DependencyInjection;
using Monitoring.Targets;

namespace Monitoring.HealthChecks;

public class HealthCheckProviderResolver : IHealthCheckProviderResolver
{
    private readonly IServiceProvider _serviceProvider;

    public HealthCheckProviderResolver(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IHealthCheckProvider Resolve(ServiceType type)
    {
        return type switch
        {
            ServiceType.Website => _serviceProvider.GetRequiredService<WebsiteCheckProvider>(),
            ServiceType.Api => _serviceProvider.GetRequiredService<ApiCheckProvider>(),
            ServiceType.Tcp => _serviceProvider.GetRequiredService<TcpCheckProvider>(),
            ServiceType.Redis => _serviceProvider.GetRequiredService<RedisCheckProvider>(),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported service type")
        };
    }
}
