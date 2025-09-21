using System;
using System.Threading;
using System.Threading.Tasks;

namespace Monitoring.HealthChecks;

public interface ISecretResolver
{
    Task<string?> ResolveAsync(string? reference, CancellationToken cancellationToken = default);
}

public sealed class EnvironmentSecretResolver : ISecretResolver
{
    public Task<string?> ResolveAsync(string? reference, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(reference))
        {
            return Task.FromResult<string?>(null);
        }

        var value = Environment.GetEnvironmentVariable(reference);
        return Task.FromResult(value);
    }
}
