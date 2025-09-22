using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monitoring.Endpoints;
using Monitoring.Options;
using Monitoring.Targets;
using Volo.Abp.Timing;

namespace Monitoring.Execution;

public sealed class HealthCheckExecutor
{
    private const int DefaultTimeoutSeconds = 15;
    private const int TimeoutBufferSeconds = 3;
    private const int BackoffBaseMilliseconds = 500;
    private const int BackoffCapMilliseconds = 30_000;

    private readonly HealthCheckProviderResolver _providerResolver;
    private readonly ITargetRunLock _runLock;
    private readonly IClock _clock;
    private readonly MonitoringOptions _options;
    private readonly ExecutionMetrics _metrics;
    private readonly ILogger<HealthCheckExecutor> _logger;

    public HealthCheckExecutor(
        HealthCheckProviderResolver providerResolver,
        ITargetRunLock runLock,
        IClock clock,
        IOptions<MonitoringOptions> options,
        ExecutionMetrics metrics,
        ILogger<HealthCheckExecutor> logger)
    {
        _providerResolver = providerResolver;
        _runLock = runLock;
        _clock = clock;
        _options = options.Value;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<HealthCheckResult> ExecuteAsync(
        MonitoringTarget target,
        string triggerSource,
        CancellationToken cancellationToken)
    {
        var timeoutSeconds = ResolveTimeoutSeconds(target);
        var attemptTimeout = TimeSpan.FromSeconds(timeoutSeconds + TimeoutBufferSeconds);
        var lockBuffer = Math.Max(1, _options.Execution.LockTtlBufferSeconds);
        var lockTtl = TimeSpan.FromSeconds((timeoutSeconds * 2) + lockBuffer);

        var lockHandle = await _runLock.TryAcquireAsync(target.Id, lockTtl, cancellationToken);
        if (lockHandle == null)
        {
            _metrics.IncrementLocksContended();
            throw new MonitoringCheckConflictException("Check already running for this target.");
        }

        await using (lockHandle)
        {
            var now = _clock.Now;
            if (target.CurrentStatus == ServiceStatus.Checking &&
                target.LastCheckedAt.HasValue &&
                now - target.LastCheckedAt.Value < attemptTimeout)
            {
                _metrics.IncrementChecksSkipped();
                var skipped = HealthCheckResult.CreateSkipped(triggerSource, "already-checking", now);
                _logger.LogInformation(
                    "Skipped check for target {TargetId} because a recent execution is still in progress",
                    target.Id);
                return skipped;
            }

            _metrics.IncrementChecksStarted();

            if (!EndpointParser.TryParse(target.Endpoint, MapEndpointType(target.Type), out var parsedEndpoint, out var parseError))
            {
                _metrics.IncrementChecksFailed();
                var failure = new HealthCheckResult(false, null, parseError ?? "Invalid endpoint", triggerSource)
                {
                    CompletedAt = now
                };
                _logger.LogWarning(
                    "Failed to parse endpoint for target {TargetId}: {Error}",
                    target.Id,
                    parseError ?? "Invalid endpoint");
                return failure;
            }

            var provider = _providerResolver.Get(parsedEndpoint.Type);
            var providerType = provider.Type;
            var configuredRetries = SettingsReader.Get<int>(target.SettingsJson, "maxRetryAttempts", 0) ?? 0;
            var maxAttempts = Math.Max(0, configuredRetries) + 1;
            HealthCheckResult? lastResult = null;

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                attemptCts.CancelAfter(attemptTimeout);

                var attemptNumber = attempt + 1;
                var stopwatch = Stopwatch.StartNew();

                try
                {
                    var providerResult = await provider.RunAsync(target, parsedEndpoint, triggerSource, attemptCts.Token);
                    stopwatch.Stop();

                    var durationMs = EnsureDuration(providerResult.ResponseTimeMs, stopwatch.ElapsedMilliseconds);
                    var completedAt = _clock.Now;
                    providerResult = providerResult with
                    {
                        ResponseTimeMs = durationMs,
                        CompletedAt = completedAt
                    };

                    var outcome = providerResult.IsSkipped
                        ? "skipped"
                        : providerResult.IsSuccess
                            ? "success"
                            : "failure";

                    _logger.LogInformation(
                        "Health check attempt {Attempt} for target {TargetId} via {Provider} ({TriggerSource}) finished {Outcome} in {Duration}ms",
                        attemptNumber,
                        target.Id,
                        providerType,
                        triggerSource,
                        outcome,
                        durationMs);

                    if (providerResult.IsSuccess)
                    {
                        _metrics.IncrementChecksSucceeded();
                        return providerResult;
                    }

                    lastResult = providerResult;
                }
                catch (OperationCanceledException) when (attemptCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    stopwatch.Stop();
                    var durationMs = EnsureDuration(null, stopwatch.ElapsedMilliseconds);
                    var completedAt = _clock.Now;
                    lastResult = new HealthCheckResult(false, durationMs, $"Timeout {timeoutSeconds}s", triggerSource)
                    {
                        CompletedAt = completedAt
                    };

                    _logger.LogWarning(
                        "Health check attempt {Attempt} for target {TargetId} via {Provider} ({TriggerSource}) timed out after {Duration}ms",
                        attemptNumber,
                        target.Id,
                        providerType,
                        triggerSource,
                        durationMs);
                }
                catch (MonitoringCheckConflictException)
                {
                    throw;
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    stopwatch.Stop();
                    var durationMs = EnsureDuration(null, stopwatch.ElapsedMilliseconds);
                    var completedAt = _clock.Now;
                    lastResult = new HealthCheckResult(false, durationMs, "Check error", triggerSource)
                    {
                        CompletedAt = completedAt
                    };

                    _logger.LogWarning(
                        ex,
                        "Health check attempt {Attempt} for target {TargetId} via {Provider} ({TriggerSource}) failed after {Duration}ms",
                        attemptNumber,
                        target.Id,
                        providerType,
                        triggerSource,
                        durationMs);
                }

                if (attemptNumber >= maxAttempts)
                {
                    break;
                }

                var backoffMs = Math.Min(
                    BackoffCapMilliseconds,
                    (int)Math.Pow(2, attempt) * BackoffBaseMilliseconds);

                _logger.LogDebug(
                    "Delaying {Delay}ms before retrying target {TargetId} via {Provider} (attempt {NextAttempt}/{MaxAttempts})",
                    backoffMs,
                    target.Id,
                    providerType,
                    attemptNumber + 1,
                    maxAttempts);

                await Task.Delay(TimeSpan.FromMilliseconds(backoffMs), cancellationToken);
            }

            _metrics.IncrementChecksFailed();
            lastResult ??= new HealthCheckResult(false, null, "Unknown failure", triggerSource)
            {
                CompletedAt = _clock.Now
            };
            return lastResult;
        }
    }

    private static int EnsureDuration(int? existing, long elapsedMilliseconds)
    {
        if (existing.HasValue)
        {
            return existing.Value;
        }

        if (elapsedMilliseconds <= 0)
        {
            return 0;
        }

        return (int)Math.Min(elapsedMilliseconds, int.MaxValue);
    }

    private int ResolveTimeoutSeconds(MonitoringTarget target)
    {
        var timeout = target.TimeoutSeconds > 0 ? target.TimeoutSeconds : DefaultTimeoutSeconds;
        if (_options.Execution.GlobalCheckTimeoutSeconds is { } global)
        {
            timeout = Math.Min(timeout, global);
        }

        return Math.Max(1, timeout);
    }

    private static EndpointType MapEndpointType(ServiceType type)
        => type switch
        {
            ServiceType.Website => EndpointType.Website,
            ServiceType.Api => EndpointType.Api,
            ServiceType.Tcp => EndpointType.Tcp,
            ServiceType.Redis => EndpointType.Redis,
            _ => EndpointType.Website
        };
}
