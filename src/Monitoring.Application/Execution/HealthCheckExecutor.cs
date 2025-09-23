using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monitoring.HealthChecks;
using Monitoring.Options;
using Monitoring.Targets;
using Volo.Abp.Timing;

namespace Monitoring.Execution;

public sealed class HealthCheckExecutor
{
    private const int DefaultTimeoutSeconds = 5;

    private readonly IHealthCheckProviderResolver _providerResolver;
    private readonly ITargetRunLock _runLock;
    private readonly IClock _clock;
    private readonly IOptionsMonitor<MonitoringExecutionOptions> _executionOptions;
    private readonly ExecutionMetrics _metrics;
    private readonly ILogger<HealthCheckExecutor> _logger;

    public HealthCheckExecutor(
        IHealthCheckProviderResolver providerResolver,
        ITargetRunLock runLock,
        IClock clock,
        IOptionsMonitor<MonitoringExecutionOptions> executionOptions,
        ExecutionMetrics metrics,
        ILogger<HealthCheckExecutor> logger)
    {
        _providerResolver = providerResolver;
        _runLock = runLock;
        _clock = clock;
        _executionOptions = executionOptions;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<HealthCheckExecutionResult> ExecuteAsync(
        MonitoringTarget target,
        string triggerSource,
        CancellationToken cancellationToken)
    {
        var options = _executionOptions.CurrentValue;
        var timeoutSeconds = ResolveTimeoutSeconds(target, options);
        var lockBufferSeconds = Math.Max(0, options.LockTtlBufferSeconds);
        var skipWindow = TimeSpan.FromSeconds(timeoutSeconds + Math.Max(5, lockBufferSeconds));
        var ttlSeconds = (timeoutSeconds * 2) + lockBufferSeconds;
        var ttl = TimeSpan.FromSeconds(ttlSeconds);

        await using var handle = await _runLock.TryAcquireAsync(target.Id, ttl, cancellationToken);
        if (handle == null)
        {
            _metrics.IncrementChecksSkipped();
            _logger.LogInformation(
                "Skipping check for target {TargetId} because the run lock is held by another node",
                target.Id);
            return HealthCheckExecutionResult.Skipped("Lock", triggerSource, _clock.Now);
        }

        var now = _clock.Now;
        if (target.CurrentStatus == ServiceStatus.Checking &&
            target.LastCheckedAt.HasValue &&
            now - target.LastCheckedAt.Value <= skipWindow)
        {
            _metrics.IncrementChecksSkipped();
            _logger.LogInformation(
                "Skipping check for target {TargetId} because a recent check is still in progress",
                target.Id);
            return HealthCheckExecutionResult.Skipped("InProgress", triggerSource, now);
        }

        _metrics.IncrementChecksStarted();

        var provider = _providerResolver.Resolve(target.Type);
        var allowedRetries = Math.Clamp(target.MaxRetryAttempts, 0, options.MaxRetryAttempts);
        var maxAttempts = Math.Max(1, allowedRetries + 1);
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);
        var baseDelaySeconds = Math.Max(1, target.RetryDelaySeconds);
        var backoffCap = Math.Max(1, options.MaxBackoffSeconds);

        HealthCheckResult? lastResult = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            attemptCts.CancelAfter(timeout);

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var result = await provider.CheckAsync(target, triggerSource, attemptCts.Token);
                stopwatch.Stop();
                var responseMs = EnsureResponseTime(result.ResponseTimeMs, stopwatch.ElapsedMilliseconds);
                result = result with { ResponseTimeMs = responseMs };

                _logger.LogInformation(
                    "Check attempt {Attempt} for target {TargetId} ({TriggerSource}) completed with outcome {Outcome} in {Duration}ms",
                    attempt,
                    target.Id,
                    triggerSource,
                    result.IsSuccess ? "Ok" : "Fail",
                    responseMs);

                if (result.IsSuccess)
                {
                    _metrics.IncrementChecksSucceeded();
                    return HealthCheckExecutionResult.Completed(result, _clock.Now, attempt);
                }

                lastResult = result;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && attemptCts.IsCancellationRequested)
            {
                stopwatch.Stop();
                var responseMs = EnsureResponseTime(null, stopwatch.ElapsedMilliseconds);
                lastResult = new HealthCheckResult(false, responseMs, $"Timeout {timeoutSeconds}s", triggerSource);
                _logger.LogWarning(
                    "Check attempt {Attempt} for target {TargetId} timed out after {Duration}ms",
                    attempt,
                    target.Id,
                    responseMs);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                stopwatch.Stop();
                var responseMs = EnsureResponseTime(null, stopwatch.ElapsedMilliseconds);
                lastResult = new HealthCheckResult(false, responseMs, "Check error", triggerSource);
                _logger.LogWarning(ex,
                    "Check attempt {Attempt} for target {TargetId} failed with an exception after {Duration}ms",
                    attempt,
                    target.Id,
                    responseMs);
            }

            if (attempt >= maxAttempts)
            {
                break;
            }

            var backoffSeconds = Math.Min(backoffCap, baseDelaySeconds * (int)Math.Pow(2, attempt - 1));
            var delay = TimeSpan.FromSeconds(backoffSeconds);
            _logger.LogDebug(
                "Waiting {Delay}s before retrying target {TargetId} (attempt {Attempt}/{Max})",
                backoffSeconds,
                target.Id,
                attempt + 1,
                maxAttempts);

            await Task.Delay(delay, cancellationToken);
        }

        _metrics.IncrementChecksFailed();
        lastResult ??= new HealthCheckResult(false, null, "Unknown failure", triggerSource);
        return HealthCheckExecutionResult.Completed(lastResult, _clock.Now, maxAttempts);
    }

    private static int EnsureResponseTime(int? existing, long elapsedMilliseconds)
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

    private int ResolveTimeoutSeconds(MonitoringTarget target, MonitoringExecutionOptions options)
    {
        var timeout = target.TimeoutSeconds > 0 ? target.TimeoutSeconds : DefaultTimeoutSeconds;
        if (options.GlobalCheckTimeoutSeconds is { } global)
        {
            timeout = Math.Min(timeout, global);
        }

        return Math.Max(1, timeout);
    }
}

public sealed record HealthCheckExecutionResult
{
    private HealthCheckExecutionResult(bool skipped, string? skipReason, HealthCheckResult result, DateTime completedAt, int attempts)
    {
        Skipped = skipped;
        SkipReason = skipReason;
        Result = result;
        CompletedAt = completedAt;
        Attempts = attempts;
    }

    public bool Skipped { get; }

    public string? SkipReason { get; }

    public HealthCheckResult Result { get; }

    public DateTime CompletedAt { get; }

    public int Attempts { get; }

    public static HealthCheckExecutionResult Skipped(string reason, string triggerSource, DateTime completedAt)
        => new(true, reason, new HealthCheckResult(false, null, reason, triggerSource), completedAt, 0);

    public static HealthCheckExecutionResult Completed(HealthCheckResult result, DateTime completedAt, int attempts)
        => new(false, null, result, completedAt, attempts);
}
