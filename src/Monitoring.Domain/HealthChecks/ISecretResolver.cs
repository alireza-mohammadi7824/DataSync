using System;

namespace Monitoring.HealthChecks;

public interface ISecretResolver
{
    string? Resolve(string? reference);
}

public sealed class EnvironmentSecretResolver : ISecretResolver
{
    public string? Resolve(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        return Environment.GetEnvironmentVariable(reference);
    }
}
