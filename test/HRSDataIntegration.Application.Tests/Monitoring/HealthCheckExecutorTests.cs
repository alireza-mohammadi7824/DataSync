using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monitoring.Endpoints;
using Monitoring.Execution;
using Monitoring.Options;
using Monitoring.Targets;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
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
            new MaintenanceRepositoryStub(),
            NullLogger<HealthCheckExecutor>.Instance);

        var target = CreateTarget();
        target.SetCurrentStatus(ServiceStatus.Online);

        await Should.ThrowAsync<MonitoringCheckConflictException>(() => executor.ExecuteAsync(target, "manual", CancellationToken.None));

        var snapshot = metrics.CreateSnapshot();
        snapshot.LocksContended.ShouldBe(1);
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
            new MaintenanceRepositoryStub(),
            NullLogger<HealthCheckExecutor>.Instance);

        var target = CreateTarget(maxRetries: 1);
        target.SetCurrentStatus(ServiceStatus.Online);

        var result = await executor.ExecuteAsync(target, "manual", CancellationToken.None);

        result.IsSkipped.ShouldBeFalse();
        result.IsSuccess.ShouldBeTrue();
        result.CompletedAt.ShouldNotBe(default);
        metrics.CreateSnapshot().ChecksSucceeded.ShouldBe(1);
    }

    private static MonitoringTarget CreateTarget(int maxRetries = 0)
    {
        var now = DateTime.UtcNow;
        var target = new MonitoringTarget(
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
        target.SetSettingsJson($"{{\"maxRetryAttempts\":{maxRetries}}}");
        return target;
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

    private sealed class MaintenanceRepositoryStub : IReadOnlyRepository<MaintenanceWindow, Guid>
    {
        private readonly List<MaintenanceWindow> _items;

        public MaintenanceRepositoryStub(IEnumerable<MaintenanceWindow>? items = null)
        {
            _items = items?.ToList() ?? new List<MaintenanceWindow>();
            AsyncExecuter = new SynchronousAsyncExecuter();
        }

        public IAsyncQueryableExecuter AsyncExecuter { get; }

        public Task<IQueryable<MaintenanceWindow>> GetQueryableAsync(bool includeDetails = true, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.AsQueryable());

        public Task<IQueryable<MaintenanceWindow>> GetQueryableAsync(CancellationToken cancellationToken)
            => Task.FromResult(_items.AsQueryable());

        public Task<IQueryable<MaintenanceWindow>> WithDetailsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_items.AsQueryable());

        public Task<IQueryable<MaintenanceWindow>> WithDetailsAsync(params Expression<Func<MaintenanceWindow, object>>[] propertySelectors)
            => Task.FromResult(_items.AsQueryable());

        public IQueryable<MaintenanceWindow> WithDetails()
            => _items.AsQueryable();

        public IQueryable<MaintenanceWindow> WithDetails(params Expression<Func<MaintenanceWindow, object>>[] propertySelectors)
            => _items.AsQueryable();

        public Task<MaintenanceWindow> GetAsync(Guid id, bool includeDetails = true, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.First(x => x.Id == id));

        public Task<MaintenanceWindow> GetAsync(Expression<Func<MaintenanceWindow, bool>> predicate, bool includeDetails = true, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.First(predicate.Compile()));

        public Task<MaintenanceWindow?> FindAsync(Guid id, bool includeDetails = true, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(x => x.Id == id));

        public Task<MaintenanceWindow?> FindAsync(Expression<Func<MaintenanceWindow, bool>> predicate, bool includeDetails = true, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(predicate.Compile()));

        public Task<List<MaintenanceWindow>> GetListAsync(bool includeDetails = false, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.ToList());

        public Task<List<MaintenanceWindow>> GetListAsync(Expression<Func<MaintenanceWindow, bool>> predicate, bool includeDetails = false, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.Where(predicate.Compile()).ToList());

        public Task<List<MaintenanceWindow>> GetPagedListAsync(int skipCount, int maxResultCount, string sorting, bool includeDetails = false, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.Skip(skipCount).Take(maxResultCount).ToList());

        public Task<long> GetCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult((long)_items.Count);

        public Task<long> GetCountAsync(Expression<Func<MaintenanceWindow, bool>> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult((long)_items.Count(predicate.Compile()));

        public Task<int> CountAsync(Expression<Func<MaintenanceWindow, bool>> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.Count(predicate.Compile()));

        public Task<bool> AnyAsync(Expression<Func<MaintenanceWindow, bool>> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.Any(predicate.Compile()));

        public Task<bool> AnyAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_items.Any());
    }

    private sealed class SynchronousAsyncExecuter : IAsyncQueryableExecuter
    {
        public Task<List<T>> ToListAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, cancellationToken).ToList());

        public Task<T[]> ToArrayAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, cancellationToken).ToArray());

        public Task<T> FirstAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, cancellationToken).First());

        public Task<T> FirstAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, predicate, cancellationToken).First());

        public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, cancellationToken).FirstOrDefault());

        public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, predicate, cancellationToken).FirstOrDefault());

        public Task<T> SingleAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, cancellationToken).Single());

        public Task<T> SingleAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, predicate, cancellationToken).Single());

        public Task<T?> SingleOrDefaultAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, cancellationToken).SingleOrDefault());

        public Task<T?> SingleOrDefaultAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, predicate, cancellationToken).SingleOrDefault());

        public Task<T> LastAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, cancellationToken).Last());

        public Task<T> LastAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, predicate, cancellationToken).Last());

        public Task<T?> LastOrDefaultAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, cancellationToken).LastOrDefault());

        public Task<T?> LastOrDefaultAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, predicate, cancellationToken).LastOrDefault());

        public Task<int> CountAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, cancellationToken).Count());

        public Task<int> CountAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, predicate, cancellationToken).Count());

        public Task<long> LongCountAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, cancellationToken).LongCount());

        public Task<long> LongCountAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, predicate, cancellationToken).LongCount());

        public Task<bool> AnyAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, cancellationToken).Any());

        public Task<bool> AnyAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, predicate, cancellationToken).Any());

        public Task<bool> AllAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, cancellationToken).All(predicate.Compile()));

        public Task<bool> ContainsAsync<T>(IQueryable<T> queryable, T item, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, cancellationToken).Contains(item));

        public Task<T> MaxAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, cancellationToken).Max());

        public Task<TResult> MaxAsync<T, TResult>(IQueryable<T> queryable, Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, selector, cancellationToken).Max());

        public Task<T> MinAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, cancellationToken).Min());

        public Task<TResult> MinAsync<T, TResult>(IQueryable<T> queryable, Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, selector, cancellationToken).Min());

        public Task<int> SumAsync(IQueryable<int> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, cancellationToken).Sum());

        public Task<int?> SumAsync(IQueryable<int?> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, cancellationToken).Sum());

        public Task<long> SumAsync(IQueryable<long> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, cancellationToken).Sum());

        public Task<long?> SumAsync(IQueryable<long?> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, cancellationToken).Sum());

        public Task<float> SumAsync(IQueryable<float> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, cancellationToken).Sum());

        public Task<float?> SumAsync(IQueryable<float?> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, cancellationToken).Sum());

        public Task<double> SumAsync(IQueryable<double> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, cancellationToken).Sum());

        public Task<double?> SumAsync(IQueryable<double?> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, cancellationToken).Sum());

        public Task<decimal> SumAsync(IQueryable<decimal> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, cancellationToken).Sum());

        public Task<decimal?> SumAsync(IQueryable<decimal?> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, cancellationToken).Sum());

        public Task<int> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, int>> selector, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, selector, cancellationToken).Sum());

        public Task<int?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, int?>> selector, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, selector, cancellationToken).Sum());

        public Task<long> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, long>> selector, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, selector, cancellationToken).Sum());

        public Task<long?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, long?>> selector, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, selector, cancellationToken).Sum());

        public Task<float> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, float>> selector, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, selector, cancellationToken).Sum());

        public Task<float?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, float?>> selector, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, selector, cancellationToken).Sum());

        public Task<double> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, double>> selector, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, selector, cancellationToken).Sum());

        public Task<double?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, double?>> selector, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, selector, cancellationToken).Sum());

        public Task<decimal> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, decimal>> selector, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, selector, cancellationToken).Sum());

        public Task<decimal?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, decimal?>> selector, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, selector, cancellationToken).Sum());

        public Task<double> AverageAsync(IQueryable<int> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, cancellationToken).Average());

        public Task<double?> AverageAsync(IQueryable<int?> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, cancellationToken).Average());

        public Task<double> AverageAsync(IQueryable<long> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, cancellationToken).Average());

        public Task<double?> AverageAsync(IQueryable<long?> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, cancellationToken).Average());

        public Task<float> AverageAsync(IQueryable<float> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, cancellationToken).Average());

        public Task<float?> AverageAsync(IQueryable<float?> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, cancellationToken).Average());

        public Task<double> AverageAsync(IQueryable<double> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, cancellationToken).Average());

        public Task<double?> AverageAsync(IQueryable<double?> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, cancellationToken).Average());

        public Task<decimal> AverageAsync(IQueryable<decimal> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, cancellationToken).Average());

        public Task<decimal?> AverageAsync(IQueryable<decimal?> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, cancellationToken).Average());

        public Task<double> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, int>> selector, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, selector, cancellationToken).Average());

        public Task<double?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, int?>> selector, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, selector, cancellationToken).Average());

        public Task<double> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, long>> selector, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, selector, cancellationToken).Average());

        public Task<double?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, long?>> selector, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, selector, cancellationToken).Average());

        public Task<float> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, float>> selector, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, selector, cancellationToken).Average());

        public Task<float?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, float?>> selector, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, selector, cancellationToken).Average());

        public Task<double> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, double>> selector, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, selector, cancellationToken).Average());

        public Task<double?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, double?>> selector, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, selector, cancellationToken).Average());

        public Task<decimal> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, decimal>> selector, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, selector, cancellationToken).Average());

        public Task<decimal?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, decimal?>> selector, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerate(queryable, selector, cancellationToken).Average());

        private static IEnumerable<T> Enumerate<T>(IQueryable<T> queryable, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return queryable.ToList();
        }

        private static IEnumerable<T> Enumerate<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken)
            => Enumerate(queryable.Where(predicate.Compile()).AsQueryable(), cancellationToken);

        private static IEnumerable<TResult> Enumerate<T, TResult>(IQueryable<T> queryable, Expression<Func<T, TResult>> selector, CancellationToken cancellationToken)
            => Enumerate(queryable, cancellationToken).Select(selector.Compile());
    }
}
