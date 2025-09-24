using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monitoring.Execution;
using Monitoring.HealthChecks;
using Monitoring.Options;
using Monitoring.Observability;
using Monitoring.Targets;
using Volo.Abp.Timing;
using Xunit;

namespace Monitoring.Application.Tests;

public sealed class Targets_CheckNow_IntegrationTests : MonitoringIntegrationTestBase
{
    private readonly ServiceProvider _serviceProvider;

    public Targets_CheckNow_IntegrationTests()
        : base(CreateServiceProvider())
    {
        _serviceProvider = (ServiceProvider)ServiceProvider;
    }

    [Fact]
    public async Task ExecuteAsync_Should_Set_Target_Status_To_Online_When_Check_Succeeds()
    {
        var executor = Get<HealthCheckExecutor>();
        var clock = Get<IClock>();

        var target = new MonitoringTarget(
            Guid.NewGuid(),
            "Example Website",
            ServiceType.Website,
            "https://example.com",
            checkIntervalSeconds: 30,
            timeoutSeconds: 3,
            maxRetryAttempts: 0,
            retryDelaySeconds: 1,
            isActive: true,
            currentStatus: ServiceStatus.Checking,
            nextDueAt: clock.Now);

        var result = await executor.ExecuteAsync(target, "test", CancellationToken.None);

        result.IsSkipped.Should().BeFalse();
        result.Result.IsSuccess.Should().BeTrue();
        target.CurrentStatus.Should().Be(ServiceStatus.Online);
    }

    public override void Dispose()
    {
        _serviceProvider.Dispose();
        base.Dispose();
    }

    private static IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.AddProvider(new NullLoggerProvider()));
        services.AddOptions();
        services.Configure<MonitoringExecutionOptions>(_ => { });
        services.AddSingleton<IClock>(new TestClock(DateTime.UtcNow));
        services.AddSingleton<ExecutionMetrics>();
        services.AddSingleton<MonitoringMetrics>();
        services.AddSingleton<SuccessHealthCheckProvider>();
        services.AddSingleton<IHealthCheckProvider>(sp => sp.GetRequiredService<SuccessHealthCheckProvider>());
        services.AddSingleton<IHealthCheckProviderResolver>(sp =>
            new FakeHealthCheckProviderResolver(sp.GetRequiredService<IHealthCheckProvider>()));
        services.AddSingleton<ITargetRunLock, NoopTargetRunLock>();
        services.AddSingleton<HealthCheckExecutor>();

        return services.BuildServiceProvider();
    }

    private sealed class TestClock : IClock
    {
        public TestClock(DateTime now)
        {
            Now = now;
        }

        public DateTime Now { get; private set; }

        public void Advance(TimeSpan delta) => Now = Now.Add(delta);
    }

    private sealed class SuccessHealthCheckProvider : IHealthCheckProvider
    {
        private readonly IClock _clock;

        public SuccessHealthCheckProvider(IClock clock)
        {
            _clock = clock;
        }

        public Task<HealthCheckResult> CheckAsync(
            MonitoringTarget target,
            string triggerSource,
            CancellationToken cancellationToken = default)
        {
            target.SetCurrentStatus(ServiceStatus.Online);
            target.SetLastCheckedAt(_clock.Now);

            var result = new HealthCheckResult(true, 25, null, triggerSource);
            return Task.FromResult(result);
        }
    }

    private sealed class FakeHealthCheckProviderResolver : IHealthCheckProviderResolver
    {
        private readonly IHealthCheckProvider _provider;

        public FakeHealthCheckProviderResolver(IHealthCheckProvider provider)
        {
            _provider = provider;
        }

        public IHealthCheckProvider Resolve(ServiceType type) => _provider;
    }

    private sealed class NoopTargetRunLock : ITargetRunLock
    {
        public Task<ITargetRunLockHandle?> TryAcquireAsync(
            Guid targetId,
            TimeSpan ttl,
            CancellationToken cancellationToken = default)
        {
            ITargetRunLockHandle handle = new NoopHandle(targetId);
            return Task.FromResult<ITargetRunLockHandle?>(handle);
        }

        private sealed class NoopHandle : ITargetRunLockHandle
        {
            public NoopHandle(Guid targetId)
            {
                TargetId = targetId;
            }

            public Guid TargetId { get; }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class NullLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => NullLogger.Instance;

        public void Dispose()
        {
        }
    }

    private sealed class NullLogger : ILogger
    {
        public static readonly NullLogger Instance = new();

        private NullLogger()
        {
        }

        public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
