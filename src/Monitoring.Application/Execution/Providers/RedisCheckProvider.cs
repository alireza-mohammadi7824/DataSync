using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Monitoring.Endpoints;
using Monitoring.Targets;
using StackExchange.Redis;

namespace Monitoring.Execution.Providers;

public sealed class RedisCheckProvider : IHealthCheckProvider
{
    private readonly ILogger<RedisCheckProvider> _logger;

    public RedisCheckProvider(ILogger<RedisCheckProvider> logger)
    {
        _logger = logger;
    }

    public EndpointType Type => EndpointType.Redis;

    public async Task<HealthCheckResult> RunAsync(
        MonitoringTarget target,
        ParsedEndpoint endpoint,
        string triggerSource,
        CancellationToken ct)
    {
        var timeout = TimeSpan.FromSeconds(target.TimeoutSeconds > 0 ? target.TimeoutSeconds : 15);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        var configuredDatabase = SettingsReader.Get<int>(target.SettingsJson, "db", endpoint.Database ?? 0);
        var database = configuredDatabase ?? endpoint.Database ?? 0;
        var username = SettingsReader.Get<string>(target.SettingsJson, "username") ?? endpoint.User;
        var password = SettingsReader.Get<string>(target.SettingsJson, "password");
        var configuredTls = SettingsReader.Get<bool>(target.SettingsJson, "tls", endpoint.Tls ?? false);
        var tls = configuredTls ?? endpoint.Tls ?? false;

        var stopwatch = Stopwatch.StartNew();
        try
        {
            (ConnectionMultiplexer connection, string host, int port) link;
            if (endpoint.IsSentinel == true)
            {
                link = await ConnectViaSentinelAsync(endpoint, username, password, tls, database, timeout, timeoutCts.Token);
            }
            else
            {
                link = await ConnectStandaloneAsync(endpoint, username, password, tls, database, timeout, timeoutCts.Token);
            }

            using (link.connection)
            {
                var db = link.connection.GetDatabase(database);
                await db.PingAsync().WaitAsync(timeoutCts.Token);
            }

            stopwatch.Stop();
            var duration = ClampDuration(stopwatch.ElapsedMilliseconds);
            _logger.LogInformation(
                "Redis check succeeded for {Host} {Port} ({Provider}) in {Duration}ms (trigger {Trigger})",
                link.host,
                link.port,
                Type,
                duration,
                triggerSource);
            return new HealthCheckResult(true, duration, $"PING in {duration}ms", triggerSource);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            stopwatch.Stop();
            var duration = ClampDuration(stopwatch.ElapsedMilliseconds);
            _logger.LogWarning(
                "Redis check timeout for {Host} {Port} ({Provider}) after {Duration}ms (trigger {Trigger})",
                endpoint.Host,
                endpoint.Port ?? 6379,
                Type,
                duration,
                triggerSource);
            return new HealthCheckResult(false, duration, $"Timeout {Math.Round(timeout.TotalSeconds)}s", triggerSource);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            stopwatch.Stop();
            var duration = ClampDuration(stopwatch.ElapsedMilliseconds);
            _logger.LogWarning(
                ex,
                "Redis check failed for {Host} {Port} ({Provider}) after {Duration}ms (trigger {Trigger})",
                endpoint.Host,
                endpoint.Port ?? 6379,
                Type,
                duration,
                triggerSource);
            return new HealthCheckResult(false, duration, "Redis error", triggerSource);
        }
    }

    private async Task<(ConnectionMultiplexer connection, string host, int port)> ConnectStandaloneAsync(
        ParsedEndpoint endpoint,
        string? username,
        string? password,
        bool tls,
        int database,
        TimeSpan timeout,
        CancellationToken ct)
    {
        var host = endpoint.Host;
        var port = endpoint.Port ?? 6379;
        var options = CreateOptions(host, port, username, password, tls, database, timeout);
        var connection = await ConnectionMultiplexer.ConnectAsync(options).WaitAsync(ct);
        return (connection, host, port);
    }

    private async Task<(ConnectionMultiplexer connection, string host, int port)> ConnectViaSentinelAsync(
        ParsedEndpoint endpoint,
        string? username,
        string? password,
        bool tls,
        int database,
        TimeSpan timeout,
        CancellationToken ct)
    {
        if (endpoint.SentinelNodes is null || endpoint.SentinelNodes.Count == 0 || string.IsNullOrWhiteSpace(endpoint.SentinelMasterName))
        {
            throw new InvalidOperationException("Sentinel configuration incomplete.");
        }

        foreach (var node in endpoint.SentinelNodes)
        {
            var sentinelOptions = new ConfigurationOptions
            {
                AbortOnConnectFail = false,
                ConnectTimeout = (int)timeout.TotalMilliseconds,
                ResponseTimeout = (int)timeout.TotalMilliseconds,
                CommandMap = CommandMap.Sentinel,
                Ssl = tls
            };
            sentinelOptions.EndPoints.Add(node.host, node.port);

            ConnectionMultiplexer? sentinel = null;
            try
            {
                sentinel = await ConnectionMultiplexer.ConnectAsync(sentinelOptions).WaitAsync(ct);
                var server = sentinel.GetServer(node.host, node.port);
                var masterEndPoint = await server
                    .SentinelGetMasterAddressByNameAsync(endpoint.SentinelMasterName!)
                    .WaitAsync(ct);

                if (masterEndPoint != null)
                {
                    if (!TryUnpackEndPoint(masterEndPoint, out var masterHost, out var masterPort))
                    {
                        continue;
                    }

                    sentinel.Dispose();
                    var options = CreateOptions(masterHost, masterPort, username, password, tls, database, timeout);
                    var connection = await ConnectionMultiplexer.ConnectAsync(options).WaitAsync(ct);
                    return (connection, masterHost, masterPort);
                }
            }
            catch
            {
                // try next sentinel
            }
            finally
            {
                sentinel?.Dispose();
            }
        }

        throw new InvalidOperationException("Unable to resolve Redis master from sentinel.");
    }

    private static bool TryUnpackEndPoint(System.Net.EndPoint endPoint, out string host, out int port)
    {
        switch (endPoint)
        {
            case System.Net.IPEndPoint ip:
                host = ip.Address.ToString();
                port = ip.Port;
                return true;
            case System.Net.DnsEndPoint dns:
                host = dns.Host;
                port = dns.Port;
                return true;
            default:
                var text = endPoint.ToString();
                if (!string.IsNullOrEmpty(text))
                {
                    var parts = text.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (parts.Length == 2 && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPort))
                    {
                        host = parts[0];
                        port = parsedPort;
                        return true;
                    }
                }

                host = string.Empty;
                port = 0;
                return false;
        }
    }

    private static ConfigurationOptions CreateOptions(
        string host,
        int port,
        string? username,
        string? password,
        bool tls,
        int database,
        TimeSpan timeout)
    {
        var options = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            ConnectTimeout = (int)timeout.TotalMilliseconds,
            ResponseTimeout = (int)timeout.TotalMilliseconds,
            DefaultDatabase = database,
            Ssl = tls
        };
        options.EndPoints.Add(host, port);
        if (!string.IsNullOrEmpty(username))
        {
            options.User = username;
        }

        if (!string.IsNullOrEmpty(password))
        {
            options.Password = password;
        }

        return options;
    }

    private static int ClampDuration(long elapsed)
    {
        if (elapsed <= 0)
        {
            return 0;
        }

        return (int)Math.Min(elapsed, int.MaxValue);
    }
}
