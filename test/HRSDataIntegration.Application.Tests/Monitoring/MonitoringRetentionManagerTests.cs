using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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

public class MonitoringRetentionManagerTests
{
    [Fact]
    public async Task PurgeAsync_ShouldRemoveExpiredHistoryAndOutages()
    {
        var now = DateTime.UtcNow;
        var targetId = Guid.NewGuid();

        var historyItems = new List<ServiceStatusHistory>
        {
            new ServiceStatusHistory(Guid.NewGuid(), targetId, ServiceStatus.Online, ServiceStatus.Offline, now.AddDays(-120), "worker", null, null),
            new ServiceStatusHistory(Guid.NewGuid(), targetId, ServiceStatus.Offline, ServiceStatus.Online, now.AddDays(-5), "worker", null, null)
        };

        var outageItems = new List<OutageWindow>
        {
            CreateClosedOutage(targetId, now.AddDays(-120), now.AddDays(-110)),
            CreateClosedOutage(targetId, now.AddDays(-5), now.AddDays(-4))
        };

        var historyRepository = new FakeRepository<ServiceStatusHistory>(historyItems, x => x.Id);
        var outageRepository = new FakeRepository<OutageWindow>(outageItems, x => x.Id);
        var options = Options.Create(new MonitoringOptions
        {
            Retention =
            {
                HistoryDays = 30,
                PurgeBatchSize = 10,
                MinOutagesPerTarget = 0
            }
        });

        var manager = new MonitoringRetentionManager(
            historyRepository,
            outageRepository,
            new FakeAsyncQueryableExecuter(),
            new TestClock(now),
            options,
            new ExecutionMetrics(),
            NullLogger<MonitoringRetentionManager>.Instance);

        var summary = await manager.PurgeAsync();

        summary.HistoryRemoved.ShouldBe(1);
        summary.OutagesRemoved.ShouldBe(1);
        historyRepository.Items.Count.ShouldBe(1);
        outageRepository.Items.Count.ShouldBe(1);
    }

    private static OutageWindow CreateClosedOutage(Guid targetId, DateTime start, DateTime end)
    {
        var outage = new OutageWindow(Guid.NewGuid(), targetId, start, 1);
        outage.Close(end);
        return outage;
    }

    private sealed class TestClock : IClock
    {
        public TestClock(DateTime now, DateTimeKind kind = DateTimeKind.Utc)
        {
            Now = now;
            Kind = kind;
        }

        public DateTime Now { get; set; }

        public bool SupportsMultipleTimezone => false;

        public DateTimeKind Kind { get; }

        public DateTime Normalize(DateTime dateTime)
        {
            return dateTime.Kind == Kind ? dateTime : DateTime.SpecifyKind(dateTime, Kind);
        }

        public DateTime? NormalizeNullable(DateTime? dateTime)
        {
            return dateTime.HasValue ? Normalize(dateTime.Value) : null;
        }
    }

    private sealed class FakeAsyncQueryableExecuter : IAsyncQueryableExecuter
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
            return queryable.AsEnumerable();
        }

        private static IEnumerable<T> Enumerate<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var compiled = predicate.Compile();
            return queryable.Where(compiled);
        }

        private static IEnumerable<TResult> Enumerate<T, TResult>(IQueryable<T> queryable, Expression<Func<T, TResult>> selector, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var compiled = selector.Compile();
            return queryable.Select(compiled);
        }
    }

    private sealed class FakeRepository<TEntity> : IRepository<TEntity, Guid>
        where TEntity : class, IEntity<Guid>, new()
    {
        private readonly List<TEntity> _items;
        private readonly Func<TEntity, Guid> _keySelector;

        public FakeRepository(IEnumerable<TEntity> items, Func<TEntity, Guid> keySelector)
        {
            _items = items.ToList();
            _keySelector = keySelector;
            AsyncExecuter = new FakeAsyncQueryableExecuter();
        }

        public List<TEntity> Items => _items;

        public IAsyncQueryableExecuter AsyncExecuter { get; }

        public bool IsChangeTrackingEnabled => false;

        public Task<IQueryable<TEntity>> GetQueryableAsync(bool includeDetails = true, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.AsQueryable());

        public Task<IQueryable<TEntity>> GetQueryableAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_items.AsQueryable());

        public Task<IQueryable<TEntity>> WithDetailsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_items.AsQueryable());

        public Task<IQueryable<TEntity>> WithDetailsAsync(params Expression<Func<TEntity, object>>[] propertySelectors)
            => Task.FromResult(_items.AsQueryable());

        public IQueryable<TEntity> AsQueryable()
            => _items.AsQueryable();

        public IQueryable<TEntity> WithDetails()
            => _items.AsQueryable();

        public IQueryable<TEntity> WithDetails(params Expression<Func<TEntity, object>>[] propertySelectors)
            => _items.AsQueryable();

        public Task<TEntity> GetAsync(Guid id, bool includeDetails = true, CancellationToken cancellationToken = default)
        {
            var entity = _items.FirstOrDefault(x => _keySelector(x) == id);
            if (entity == null)
            {
                throw new KeyNotFoundException($"Entity {typeof(TEntity).Name} with id {id} was not found");
            }

            return Task.FromResult(entity);
        }

        public Task<TEntity> GetAsync(Expression<Func<TEntity, bool>> predicate, bool includeDetails = true, CancellationToken cancellationToken = default)
        {
            var compiled = predicate.Compile();
            var entity = _items.FirstOrDefault(compiled);
            if (entity == null)
            {
                throw new KeyNotFoundException("Entity not found");
            }

            return Task.FromResult(entity);
        }

        public Task<TEntity?> FindAsync(Guid id, bool includeDetails = true, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(x => _keySelector(x) == id));

        public Task<TEntity?> FindAsync(Expression<Func<TEntity, bool>> predicate, bool includeDetails = false, CancellationToken cancellationToken = default)
        {
            var compiled = predicate.Compile();
            return Task.FromResult(_items.FirstOrDefault(compiled));
        }

        public Task<List<TEntity>> GetListAsync(bool includeDetails = false, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.ToList());

        public Task<List<TEntity>> GetListAsync(Expression<Func<TEntity, bool>> predicate, bool includeDetails = false, CancellationToken cancellationToken = default)
        {
            var compiled = predicate.Compile();
            return Task.FromResult(_items.Where(compiled).ToList());
        }

        public Task<List<TEntity>> GetPagedListAsync(int skipCount, int maxResultCount, string sorting, bool includeDetails = false, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = _items.Skip(skipCount).Take(maxResultCount).ToList();
            return Task.FromResult(page);
        }

        public Task<long> GetCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult((long)_items.Count);

        public Task<long> GetCountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
        {
            var compiled = predicate.Compile();
            return Task.FromResult((long)_items.Count(compiled));
        }

        public Task<int> CountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
        {
            var compiled = predicate.Compile();
            return Task.FromResult(_items.Count(compiled));
        }

        public Task DeleteAsync(Guid id, bool autoSave = false, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entity = _items.FirstOrDefault(x => _keySelector(x) == id);
            if (entity != null)
            {
                _items.Remove(entity);
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _items.Remove(entity);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Expression<Func<TEntity, bool>> predicate, bool autoSave = false, CancellationToken cancellationToken = default)
        {
            var compiled = predicate.Compile();
            _items.RemoveAll(x => compiled(x));
            return Task.CompletedTask;
        }

        public Task DeleteDirectAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
        {
            var compiled = predicate.Compile();
            _items.RemoveAll(x => compiled(x));
            return Task.CompletedTask;
        }

        public Task DeleteManyAsync(IEnumerable<TEntity> entities, bool autoSave = false, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var entity in entities)
            {
                _items.Remove(entity);
            }

            return Task.CompletedTask;
        }

        public Task DeleteManyAsync(IEnumerable<Guid> ids, bool autoSave = false, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var set = new HashSet<Guid>(ids);
            _items.RemoveAll(x => set.Contains(_keySelector(x)));
            return Task.CompletedTask;
        }

        public Task DeleteManyAsync(Expression<Func<TEntity, bool>> predicate, bool autoSave = false, CancellationToken cancellationToken = default)
        {
            var compiled = predicate.Compile();
            _items.RemoveAll(x => compiled(x));
            return Task.CompletedTask;
        }

        public Task<TEntity> InsertAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_items.Contains(entity))
            {
                _items.Add(entity);
            }

            return Task.FromResult(entity);
        }

        public Task<TEntity> InsertAsync(TEntity entity, bool autoSave, CancellationToken cancellationToken, bool? autoSaveChanges)
            => InsertAsync(entity, autoSave, cancellationToken);

        public Task InsertManyAsync(IEnumerable<TEntity> entities, bool autoSave = false, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var entity in entities)
            {
                if (!_items.Contains(entity))
                {
                    _items.Add(entity);
                }
            }

            return Task.CompletedTask;
        }

        public Task InsertManyAsync(IEnumerable<TEntity> entities, bool autoSave, CancellationToken cancellationToken, bool? autoSaveChanges)
            => InsertManyAsync(entities, autoSave, cancellationToken);

        public Task<TEntity> InsertOrUpdateAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var id = _keySelector(entity);
            var existing = _items.FirstOrDefault(x => _keySelector(x) == id);
            if (existing != null)
            {
                _items.Remove(existing);
            }

            if (!_items.Contains(entity))
            {
                _items.Add(entity);
            }

            return Task.FromResult(entity);
        }

        public Task<TEntity> InsertOrUpdateAsync(TEntity entity, bool autoSave, CancellationToken cancellationToken, bool? autoSaveChanges)
            => InsertOrUpdateAsync(entity, autoSave, cancellationToken);

        public Task<TEntity> UpdateAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var id = _keySelector(entity);
            var existing = _items.FirstOrDefault(x => _keySelector(x) == id);
            if (existing != null)
            {
                _items.Remove(existing);
            }

            _items.Add(entity);
            return Task.FromResult(entity);
        }

        public Task<TEntity> UpdateAsync(TEntity entity, bool autoSave, CancellationToken cancellationToken, bool? autoSaveChanges)
            => UpdateAsync(entity, autoSave, cancellationToken);

        public Task UpdateManyAsync(IEnumerable<TEntity> entities, bool autoSave = false, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var entity in entities)
            {
                var id = _keySelector(entity);
                var existing = _items.FirstOrDefault(x => _keySelector(x) == id);
                if (existing != null)
                {
                    _items.Remove(existing);
                }

                _items.Add(entity);
            }

            return Task.CompletedTask;
        }

        public Task UpdateManyAsync(IEnumerable<TEntity> entities, bool autoSave, CancellationToken cancellationToken, bool? autoSaveChanges)
            => UpdateManyAsync(entities, autoSave, cancellationToken);

        public TEntity Attach(TEntity entity) => entity;

        public void Detach(TEntity entity)
        {
        }

        public Task EnsureCollectionLoadedAsync<TProperty>(TEntity entity, Expression<Func<TEntity, IEnumerable<TProperty>>> propertyExpression, CancellationToken cancellationToken = default)
            where TProperty : class
            => Task.CompletedTask;

        public Task EnsurePropertyLoadedAsync<TProperty>(TEntity entity, Expression<Func<TEntity, TProperty>> propertyExpression, CancellationToken cancellationToken = default)
            where TProperty : class
            => Task.CompletedTask;
    }

}
}
