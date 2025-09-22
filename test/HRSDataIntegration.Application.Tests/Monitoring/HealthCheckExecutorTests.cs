using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monitoring.Endpoints;
using Monitoring.Execution;
using Monitoring.Options;
using Monitoring.Targets;
using Shouldly;
using Volo.Abp.Timing;
using Xunit;

namespace HRSDataIntegration.Application.Tests.Monitoring;

public class HealthCheckExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldSkip_When_Lock_Unavailable()
    {
        var options = Options.Create(new MonitoringOptions());
        var metrics = new ExecutionMetrics();
        var executor = new HealthCheckExecutor(
            new HealthCheckProviderResolver(new[] { new SequenceProvider(new Queue<HealthCheckResult>()) }),
            new FailingRunLock(),
            new TestClock(DateTime.UtcNow),
            options,
            metrics,
            NullLogger<HealthCheckExecutor>.Instance);

        var target = CreateTarget();
        target.SetCurrentStatus(ServiceStatus.Online);

        var result = await executor.ExecuteAsync(target, "manual", CancellationToken.None);

        result.Skipped.ShouldBeTrue();
        metrics.CreateSnapshot().ChecksSkipped.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRetry_And_Succeed()
    {
        var options = Options.Create(new MonitoringOptions
        {
            Execution = { LockTtlBufferSeconds = 1 }
        });
        var metrics = new ExecutionMetrics();
        var results = new Queue<HealthCheckResult>(new[]
        {
            new HealthCheckResult(false, 10, "fail", "manual"),
            new HealthCheckResult(true, 8, null, "manual")
        });

        var executor = new HealthCheckExecutor(
            new HealthCheckProviderResolver(new[] { new SequenceProvider(results) }),
            new SuccessfulRunLock(),
            new TestClock(DateTime.UtcNow),
            options,
            metrics,
            NullLogger<HealthCheckExecutor>.Instance);

        var target = CreateTarget(maxRetries: 1);
        target.SetCurrentStatus(ServiceStatus.Online);

        var result = await executor.ExecuteAsync(target, "manual", CancellationToken.None);

        result.Skipped.ShouldBeFalse();
        result.Result.IsSuccess.ShouldBeTrue();
        metrics.CreateSnapshot().ChecksSucceeded.ShouldBe(1);
    }

    private static MonitoringTarget CreateTarget(int maxRetries = 0)
    {
        var now = DateTime.UtcNow;
        return new MonitoringTarget(
            Guid.NewGuid(),
            "api",
            ServiceType.Api,
            "https://example.com",
            60,
            5,
            maxRetries,
            1,
            true,
            ServiceStatus.Online,
            now);
    }

    private sealed class TestClock : IClock
    {
        public TestClock(DateTime now)
        {
            Now = now;
        }

        public DateTime Now { get; set; }

        public bool SupportsMultipleTimezone => false;

        public DateTimeKind Kind { get; init; } = DateTimeKind.Utc;

        public DateTime Normalize(DateTime dateTime)
        {
            return DateTime.SpecifyKind(dateTime, Kind);
        }

        public DateTime? NormalizeNullable(DateTime? dateTime)
        {
            return dateTime.HasValue ? Normalize(dateTime.Value) : null;
        }
    }

    private sealed class SequenceProvider : IHealthCheckProvider
    {
        private readonly Queue<HealthCheckResult> _results;

        public SequenceProvider(Queue<HealthCheckResult> results)
        {
            _results = results;
        }

        public EndpointType Type => EndpointType.Api;

        public Task<HealthCheckResult> RunAsync(MonitoringTarget target, ParsedEndpoint endpoint, string triggerSource, CancellationToken ct)
        {
            return Task.FromResult(_results.Dequeue());
        }
    }

    private sealed class FailingRunLock : ITargetRunLock
    {
        public Task<ITargetRunLockHandle?> TryAcquireAsync(Guid targetId, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ITargetRunLockHandle?>(null);
        }
    }

    private sealed class SuccessfulRunLock : ITargetRunLock
    {
        public Task<ITargetRunLockHandle?> TryAcquireAsync(Guid targetId, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ITargetRunLockHandle?>(new Handle(targetId));
        }

        private sealed class Handle : ITargetRunLockHandle
        {
            public Handle(Guid targetId)
            {
                TargetId = targetId;
            }

            public Guid TargetId { get; }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
