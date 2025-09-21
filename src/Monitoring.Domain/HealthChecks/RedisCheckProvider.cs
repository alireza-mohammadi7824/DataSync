using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Monitoring.Targets;
using StackExchange.Redis;

namespace Monitoring.HealthChecks;

public class RedisCheckProvider : IHealthCheckProvider
{
    private const int DefaultTimeoutSeconds = 5;

    private readonly ISecretResolver _secretResolver;

    public RedisCheckProvider(ISecretResolver secretResolver)
    {
        _secretResolver = secretResolver;
    }

    public async Task<HealthCheckResult> CheckAsync(
        MonitoringTarget target,
        string triggerSource,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var settings = HealthCheckSettingsParser.ParseRedis(target.SettingsJson);
        var timeoutSeconds = target.TimeoutSeconds > 0 ? target.TimeoutSeconds : DefaultTimeoutSeconds;
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);

        var username = ResolveSecret(settings.UsernameRef);
        if (RequiresSecret(settings.UsernameRef) && string.IsNullOrEmpty(username))
        {
            return new HealthCheckResult(false, null, "Auth missing", triggerSource);
        }

        var password = ResolveSecret(settings.PasswordRef);
        if (RequiresSecret(settings.PasswordRef) && string.IsNullOrEmpty(password))
        {
            return new HealthCheckResult(false, null, "Auth missing", triggerSource);
        }

        var stopwatch = Stopwatch.StartNew();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            var (options, serverEndpoint, configurationError) = await CreateConfigurationOptionsAsync(
                target,
                settings,
                timeout,
                timeoutCts.Token);

            if (options is null)
            {
                stopwatch.Stop();
                var elapsed = stopwatch.ElapsedMilliseconds > 0
                    ? (int?)Math.Round(stopwatch.Elapsed.TotalMilliseconds)
                    : null;

                return new HealthCheckResult(false, elapsed, configurationError ?? "Redis error", triggerSource);
            }

            ApplyCredentials(options, username, password);

            using var connection = await ConnectionMultiplexer
                .ConnectAsync(options)
                .WaitAsync(timeoutCts.Token);

            var database = connection.GetDatabase(settings.Database);

            if (settings.PingCheck)
            {
                await database.PingAsync();
            }

            if (settings.AllowAdmin && !IsAnyRole(settings.ExpectedRole))
            {
                var roleMatches = await CheckRoleAsync(
                    connection,
                    serverEndpoint,
                    settings.ExpectedRole,
                    timeoutCts.Token);

                if (!roleMatches)
                {
                    stopwatch.Stop();
                    var roleElapsed = (int)Math.Round(stopwatch.Elapsed.TotalMilliseconds);
                    return new HealthCheckResult(false, roleElapsed, "Role mismatch", triggerSource);
                }
            }

            stopwatch.Stop();
            var elapsedMs = (int)Math.Round(stopwatch.Elapsed.TotalMilliseconds);

            if (settings.LatencyThresholdMs > 0 && elapsedMs > settings.LatencyThresholdMs)
            {
                return new HealthCheckResult(false, elapsedMs, $"High latency {elapsedMs}ms", triggerSource);
            }

            return new HealthCheckResult(true, elapsedMs, null, triggerSource);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            var elapsed = stopwatch.ElapsedMilliseconds > 0
                ? (int?)Math.Round(stopwatch.Elapsed.TotalMilliseconds)
                : null;

            return new HealthCheckResult(false, elapsed, $"Timeout {timeoutSeconds}s", triggerSource);
        }
        catch (RedisTimeoutException)
        {
            stopwatch.Stop();
            var elapsed = stopwatch.ElapsedMilliseconds > 0
                ? (int?)Math.Round(stopwatch.Elapsed.TotalMilliseconds)
                : null;

            return new HealthCheckResult(false, elapsed, $"Timeout {timeoutSeconds}s", triggerSource);
        }
        catch (RedisConnectionException ex) when (TryMapSocketError(ex.InnerException, timeoutSeconds, out var summary))
        {
            stopwatch.Stop();
            var elapsed = stopwatch.ElapsedMilliseconds > 0
                ? (int?)Math.Round(stopwatch.Elapsed.TotalMilliseconds)
                : null;

            return new HealthCheckResult(false, elapsed, summary, triggerSource);
        }
        catch (SocketException ex) when (TryMapSocketError(ex, timeoutSeconds, out var summary))
        {
            stopwatch.Stop();
            var elapsed = stopwatch.ElapsedMilliseconds > 0
                ? (int?)Math.Round(stopwatch.Elapsed.TotalMilliseconds)
                : null;

            return new HealthCheckResult(false, elapsed, summary, triggerSource);
        }
        catch (RedisConnectionException)
        {
            stopwatch.Stop();
            var elapsed = stopwatch.ElapsedMilliseconds > 0
                ? (int?)Math.Round(stopwatch.Elapsed.TotalMilliseconds)
                : null;

            return new HealthCheckResult(false, elapsed, "Redis error", triggerSource);
        }
        catch (Exception)
        {
            stopwatch.Stop();
            var elapsed = stopwatch.ElapsedMilliseconds > 0
                ? (int?)Math.Round(stopwatch.Elapsed.TotalMilliseconds)
                : null;

            return new HealthCheckResult(false, elapsed, "Redis error", triggerSource);
        }
    }

    private static bool RequiresSecret(string? reference)
    {
        return !string.IsNullOrWhiteSpace(reference);
    }

    private string? ResolveSecret(string? reference)
    {
        return _secretResolver.Resolve(reference);
    }

    private static void ApplyCredentials(
        ConfigurationOptions options,
        string? username,
        string? password)
    {
        if (!string.IsNullOrEmpty(username))
        {
            options.User = username;
        }

        if (!string.IsNullOrEmpty(password))
        {
            options.Password = password;
        }
    }

    private static bool TryMapSocketError(Exception? exception, int timeoutSeconds, out string summary)
    {
        summary = "Redis error";

        if (exception is not SocketException socketException)
        {
            return false;
        }

        summary = socketException.SocketErrorCode switch
        {
            SocketError.ConnectionRefused => "Connect refused",
            SocketError.HostNotFound or SocketError.NoData => "DNS error",
            SocketError.TimedOut => $"Timeout {timeoutSeconds}s",
            _ => "Redis error"
        };

        return true;
    }

    private async Task<(ConfigurationOptions? Options, EndPoint? ServerEndpoint, string? Error)> CreateConfigurationOptionsAsync(
        MonitoringTarget target,
        RedisSettings settings,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var mode = (settings.Mode ?? "standalone").Trim();

        if (string.Equals(mode, "sentinel", StringComparison.OrdinalIgnoreCase))
        {
            return await CreateSentinelConfigurationAsync(settings, timeout, cancellationToken);
        }

        var endpoints = ResolveEndpoints(settings.Endpoints, target.Endpoint);
        if (endpoints.Count == 0)
        {
            return (null, null, "Invalid host/port");
        }

        var options = CreateBaseOptions(settings, timeout);
        foreach (var endpoint in endpoints)
        {
            options.EndPoints.Add(endpoint.Host, endpoint.Port);
        }

        return (options, options.EndPoints.FirstOrDefault(), null);
    }

    private async Task<(ConfigurationOptions? Options, EndPoint? ServerEndpoint, string? Error)> CreateSentinelConfigurationAsync(
        RedisSettings settings,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (settings.Sentinels is null || settings.Sentinels.Length == 0)
        {
            return (null, null, "Sentinel master not found");
        }

        if (string.IsNullOrWhiteSpace(settings.SentinelMasterName))
        {
            return (null, null, "Sentinel master not found");
        }

        foreach (var sentinel in settings.Sentinels)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var master = await ResolveSentinelMasterAsync(
                    sentinel,
                    settings.SentinelMasterName,
                    settings.UseTls,
                    timeout,
                    cancellationToken);

                if (master is null)
                {
                    continue;
                }

                var (masterHost, masterPort) = master.Value;
                var options = CreateBaseOptions(settings, timeout);
                options.EndPoints.Add(masterHost, masterPort);

                return (options, new DnsEndPoint(masterHost, masterPort), null);
            }
            catch (RedisConnectionException)
            {
                continue;
            }
            catch (RedisTimeoutException)
            {
                continue;
            }
            catch (RedisException)
            {
                continue;
            }
        }

        return (null, null, "Sentinel master not found");
    }

    private async Task<(string Host, int Port)?> ResolveSentinelMasterAsync(
        string sentinelEndpoint,
        string masterName,
        bool useTls,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (!TryParseSentinelEndpoint(sentinelEndpoint, out var sentinelHost, out var sentinelPort))
        {
            return null;
        }

        var configuration = new ConfigurationOptions
        {
            AllowAdmin = true,
            AbortOnConnectFail = false,
            ConnectTimeout = (int)Math.Max(1, timeout.TotalMilliseconds),
            SyncTimeout = (int)Math.Max(1, timeout.TotalMilliseconds),
            ResponseTimeout = (int)Math.Max(1, timeout.TotalMilliseconds),
            Ssl = useTls
        };

        configuration.EndPoints.Add(sentinelHost, sentinelPort);

        using var connection = await ConnectionMultiplexer
            .ConnectAsync(configuration)
            .WaitAsync(cancellationToken);

        var server = connection.GetServer(sentinelHost, sentinelPort);
        var result = await server.ExecuteAsync(
            "SENTINEL",
            "get-master-addr-by-name",
            masterName);

        if (result.IsNull || result.Type != ResultType.MultiBulk)
        {
            return null;
        }

        var values = (RedisResult[])result;
        if (values.Length < 2)
        {
            return null;
        }

        if (values[0].IsNull || values[1].IsNull)
        {
            return null;
        }

        var hostCandidate = values[0].ToString();
        var portCandidate = values[1].ToString();

        if (string.IsNullOrWhiteSpace(hostCandidate))
        {
            return null;
        }

        if (!int.TryParse(portCandidate, NumberStyles.Integer, CultureInfo.InvariantCulture, out var masterPort))
        {
            return null;
        }

        if (masterPort is < 1 or > 65535)
        {
            return null;
        }

        return (hostCandidate, masterPort);
    }

    private static bool TryParseSentinelEndpoint(string value, out string host, out int port)
    {
        host = string.Empty;
        port = 0;

        var parts = value.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPort))
        {
            return false;
        }

        if (parsedPort is < 1 or > 65535)
        {
            return false;
        }

        host = parts[0].Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        port = parsedPort;
        return true;
    }

    private static ConfigurationOptions CreateBaseOptions(RedisSettings settings, TimeSpan timeout)
    {
        var options = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            ConnectTimeout = (int)Math.Max(1, timeout.TotalMilliseconds),
            SyncTimeout = (int)Math.Max(1, timeout.TotalMilliseconds),
            ResponseTimeout = (int)Math.Max(1, timeout.TotalMilliseconds),
            DefaultDatabase = settings.Database,
            AllowAdmin = settings.AllowAdmin,
            Ssl = settings.UseTls
        };

        if (string.Equals(settings.Mode, "cluster", StringComparison.OrdinalIgnoreCase))
        {
            options.TieBreaker = string.Empty;
        }

        return options;
    }

    private static List<(string Host, int Port)> ResolveEndpoints(string[]? configuredEndpoints, string? fallback)
    {
        var endpoints = new List<(string Host, int Port)>();

        if (configuredEndpoints is not null)
        {
            foreach (var endpoint in configuredEndpoints)
            {
                if (EndpointParser.TryParseHostPort(endpoint, out var host, out var port))
                {
                    endpoints.Add((host, port));
                }
                else if (TryParseEndpoint(endpoint, out host, out port))
                {
                    endpoints.Add((host, port));
                }
            }
        }

        if (endpoints.Count > 0)
        {
            return endpoints;
        }

        if (!string.IsNullOrWhiteSpace(fallback))
        {
            foreach (var candidate in SplitEndpointList(fallback))
            {
                if (EndpointParser.TryParseHostPort(candidate, out var host, out var port))
                {
                    endpoints.Add((host, port));
                }
                else if (TryParseEndpoint(candidate, out host, out port))
                {
                    endpoints.Add((host, port));
                }
            }
        }

        return endpoints;
    }

    private static IEnumerable<string> SplitEndpointList(string value)
    {
        return value
            .Split(new[] { ',', ';', '|', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(v => v.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v));
    }

    private static bool TryParseEndpoint(string? value, out string host, out int port)
    {
        host = string.Empty;
        port = 0;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value;
        if (!value.Contains("://", StringComparison.Ordinal))
        {
            candidate = $"redis://{value}";
        }

        if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri) &&
            !string.IsNullOrWhiteSpace(uri.Host) &&
            uri.Port > 0)
        {
            host = uri.Host;
            port = uri.Port;
            return true;
        }

        return false;
    }

    private static async Task<bool> CheckRoleAsync(
        ConnectionMultiplexer connection,
        EndPoint? preferredEndpoint,
        string expectedRole,
        CancellationToken cancellationToken)
    {
        var normalized = (expectedRole ?? string.Empty).Trim();
        var endpoints = connection.GetEndPoints();
        if (endpoints.Length == 0)
        {
            return false;
        }

        var ordered = preferredEndpoint is not null
            ? new[] { preferredEndpoint }.Concat(endpoints.Where(e => !Equals(e, preferredEndpoint)))
            : endpoints;

        foreach (var endpoint in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var server = connection.GetServer(endpoint);
            if (!server.IsConnected)
            {
                continue;
            }

            try
            {
                var role = await GetServerRoleAsync(server);
                if (role is null)
                {
                    continue;
                }

                return MatchesRole(role, normalized);
            }
            catch (RedisServerException)
            {
                continue;
            }
        }

        return false;
    }

    private static async Task<string?> GetServerRoleAsync(IServer server)
    {
        var result = await server.ExecuteAsync("ROLE");
        if (result.Type == ResultType.MultiBulk)
        {
            var items = (RedisResult[])result;
            if (items.Length > 0)
            {
                return items[0].ToString();
            }
        }

        return result.ToString();
    }

    private static bool MatchesRole(string role, string expectedRole)
    {
        if (string.Equals(expectedRole, "master", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(role, "master", StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(expectedRole, "replica", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(role, "replica", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(role, "slave", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(role, "secondary", StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    private static bool IsAnyRole(string? expectedRole)
    {
        return string.IsNullOrWhiteSpace(expectedRole) ||
               string.Equals(expectedRole, "any", StringComparison.OrdinalIgnoreCase);
    }
}
