using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Monitoring.Targets;

namespace Monitoring.HealthChecks;

public class TcpCheckProvider : IHealthCheckProvider
{
    private const int DefaultTimeoutSeconds = 5;

    public async Task<HealthCheckResult> CheckAsync(
        MonitoringTarget target,
        string triggerSource,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var settings = HealthCheckSettingsParser.ParseTcp(target.SettingsJson);
        ResolveEndpoint(target.Endpoint, settings, out var host, out var port);

        if (string.IsNullOrWhiteSpace(host) || port is null || port is < 1 or > 65535)
        {
            return new HealthCheckResult(false, null, "Invalid host/port", triggerSource);
        }

        var timeoutSeconds = target.TimeoutSeconds > 0 ? target.TimeoutSeconds : DefaultTimeoutSeconds;
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);

        using var tcpClient = new TcpClient();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await tcpClient.ConnectAsync(host!, port.Value, timeoutCts.Token);

            stopwatch.Stop();
            var elapsed = (int)Math.Round(stopwatch.Elapsed.TotalMilliseconds);

            return new HealthCheckResult(true, elapsed, null, triggerSource);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            var elapsed = stopwatch.ElapsedMilliseconds > 0
                ? (int?)Math.Round(stopwatch.Elapsed.TotalMilliseconds)
                : null;

            return new HealthCheckResult(false, elapsed, $"Timeout {timeoutSeconds}s", triggerSource);
        }
        catch (SocketException ex)
        {
            stopwatch.Stop();
            var elapsed = stopwatch.ElapsedMilliseconds > 0
                ? (int?)Math.Round(stopwatch.Elapsed.TotalMilliseconds)
                : null;

            var summary = ex.SocketErrorCode switch
            {
                SocketError.HostNotFound or SocketError.NoData => "Host not found",
                SocketError.ConnectionRefused => "Connect refused",
                SocketError.TimedOut => $"Timeout {timeoutSeconds}s",
                _ => "TCP error"
            };

            return new HealthCheckResult(false, elapsed, summary, triggerSource);
        }
        catch (Exception)
        {
            stopwatch.Stop();
            var elapsed = stopwatch.ElapsedMilliseconds > 0
                ? (int?)Math.Round(stopwatch.Elapsed.TotalMilliseconds)
                : null;

            return new HealthCheckResult(false, elapsed, "TCP error", triggerSource);
        }
    }

    private static void ResolveEndpoint(string? endpoint, TcpSettings settings, out string? host, out int? port)
    {
        host = null;
        port = null;

        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            var trimmed = endpoint.Trim();

            if (TryResolveHostPort(trimmed, out var parsedHost, out var parsedPort))
            {
                host = parsedHost;
                port = parsedPort;
            }
        }

        if (string.IsNullOrWhiteSpace(host) && !string.IsNullOrWhiteSpace(settings.Host))
        {
            host = settings.Host.Trim();
        }

        if (port is null && settings.Port.HasValue)
        {
            port = settings.Port.Value;
        }
    }

    private static bool TryResolveHostPort(string value, out string? host, out int? port)
    {
        host = null;
        port = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (EndpointParser.TryParseHostPort(value, out var parsedHost, out var parsedPort))
        {
            host = parsedHost;
            port = parsedPort;
            return true;
        }

        var candidate = value;

        if (!value.Contains("://", StringComparison.Ordinal))
        {
            candidate = $"tcp://{value}";
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Host))
        {
            return false;
        }

        host = uri.Host;
        port = uri.Port > 0 ? uri.Port : null;

        return port.HasValue;
    }
}
