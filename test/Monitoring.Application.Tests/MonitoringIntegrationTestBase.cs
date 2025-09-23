using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Monitoring.Application.Tests;

public abstract class MonitoringIntegrationTestBase : IDisposable
{
    protected MonitoringIntegrationTestBase(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    protected IServiceProvider ServiceProvider { get; }

    protected T Get<T>() where T : notnull
    {
        return ServiceProvider.GetRequiredService<T>();
    }

    protected virtual ValueTask ResetAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }

    public virtual void Dispose()
    {
    }
}
