using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Monitoring.Endpoints;
using Monitoring.Targets;

namespace Monitoring.Execution.Providers;

public sealed class TcpCheckProvider : IHealthCheckProvider
{
    private readonly ILogger<TcpCheckProvider> _logger;

    public TcpCheckProvider(ILogger<TcpCheckProvider> logger)
    {
        _logger = logger;
    }

    public EndpointType Type => EndpointType.Tcp;

    public async Task<HealthCheckResult> RunAsync(
        MonitoringTarget target,
        ParsedEndpoint endpoint,
        string triggerSource,
        CancellationToken ct)
    {
        if (endpoint.Port is null)
        {
            return new HealthCheckResult(false, null, "Port required", triggerSource);
        }

        var timeout = TimeSpan.FromSeconds(target.TimeoutSeconds > 0 ? target.TimeoutSeconds : 15);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        var sendPayload = SettingsReader.Get<string>(target.SettingsJson, "send");
        var expectContains = SettingsReader.Get<string>(target.SettingsJson, "expectContains");
        var readTimeoutMs = SettingsReader.Get<int?>(target.SettingsJson, "readTimeoutMs", 2000) ?? 2000;
        if (readTimeoutMs <= 0)
        {
            readTimeoutMs = 2000;
        }

        using var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp)
        {
            DualMode = true
        };

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await socket.ConnectAsync(endpoint.Host, endpoint.Port.Value, timeoutCts.Token);

            if (!string.IsNullOrEmpty(sendPayload))
            {
                var bytes = Encoding.UTF8.GetBytes(sendPayload);
                await socket.SendAsync(bytes, SocketFlags.None, timeoutCts.Token);

                if (!string.IsNullOrEmpty(expectContains))
                {
                    using var readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    readCts.CancelAfter(TimeSpan.FromMilliseconds(readTimeoutMs));

                    var buffer = new byte[1024];
                    try
                    {
                        var received = await socket.ReceiveAsync(buffer, SocketFlags.None, readCts.Token);
                        var text = Encoding.UTF8.GetString(buffer, 0, received);
                        if (received == 0 || !text.Contains(expectContains, StringComparison.Ordinal))
                        {
                            stopwatch.Stop();
                            var duration = ClampDuration(stopwatch.ElapsedMilliseconds);
                            _logger.LogWarning(
                                "TCP check response mismatch for {Host} {Port} ({Provider}) in {Duration}ms (trigger {Trigger})",
                                endpoint.Host,
                                endpoint.Port,
                                Type,
                                duration,
                                triggerSource);
                            return new HealthCheckResult(false, duration, "Response mismatch", triggerSource);
                        }
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested && readCts.IsCancellationRequested)
                    {
                        stopwatch.Stop();
                        var duration = ClampDuration(stopwatch.ElapsedMilliseconds);
                        _logger.LogWarning(
                            "TCP check read timeout for {Host} {Port} ({Provider}) after {Duration}ms (trigger {Trigger})",
                            endpoint.Host,
                            endpoint.Port,
                            Type,
                            duration,
                            triggerSource);
                        return new HealthCheckResult(false, duration, "Read timeout", triggerSource);
                    }
                }
            }

            stopwatch.Stop();
            var elapsed = ClampDuration(stopwatch.ElapsedMilliseconds);
            _logger.LogInformation(
                "TCP check succeeded for {Host} {Port} ({Provider}) in {Duration}ms (trigger {Trigger})",
                endpoint.Host,
                endpoint.Port,
                Type,
                elapsed,
                triggerSource);
            return new HealthCheckResult(true, elapsed, $"Connected in {elapsed}ms", triggerSource);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            stopwatch.Stop();
            var elapsed = ClampDuration(stopwatch.ElapsedMilliseconds);
            _logger.LogWarning(
                "TCP check timeout for {Host} {Port} ({Provider}) after {Duration}ms (trigger {Trigger})",
                endpoint.Host,
                endpoint.Port,
                Type,
                elapsed,
                triggerSource);
            return new HealthCheckResult(false, elapsed, $"Timeout {Math.Round(timeout.TotalSeconds)}s", triggerSource);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            stopwatch.Stop();
            var elapsed = ClampDuration(stopwatch.ElapsedMilliseconds);
            _logger.LogWarning(
                ex,
                "TCP check failed for {Host} {Port} ({Provider}) after {Duration}ms (trigger {Trigger})",
                endpoint.Host,
                endpoint.Port,
                Type,
                elapsed,
                triggerSource);
            return new HealthCheckResult(false, elapsed, "TCP error", triggerSource);
        }
        finally
        {
            if (socket.Connected)
            {
                try
                {
                    socket.Shutdown(SocketShutdown.Both);
                }
                catch
                {
                    // ignore shutdown exceptions
                }
            }
        }
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
