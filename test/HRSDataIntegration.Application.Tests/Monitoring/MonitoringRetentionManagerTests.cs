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

    private sealed class FakeAsyncQueryableExecuter : IAsyncQueryableExecuter
    {
        public Task<List<T>> ToListAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(queryable.ToList());

        public Task<T> FirstOrDefaultAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(queryable.FirstOrDefault());

        public Task<T> FirstOrDefaultAsync<T>(
            IQueryable<T> queryable,
            Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default)
            => Task.FromResult(queryable.FirstOrDefault(predicate));

        public Task<int> CountAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(queryable.Count());

        public Task<int> CountAsync<T>(
            IQueryable<T> queryable,
            Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default)
            => Task.FromResult(queryable.Count(predicate));

        public Task<long> LongCountAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(queryable.LongCount());

        public Task<long> LongCountAsync<T>(
            IQueryable<T> queryable,
            Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default)
            => Task.FromResult(queryable.LongCount(predicate));

        public Task<bool> AnyAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(queryable.Any());

        public Task<bool> AnyAsync<T>(
            IQueryable<T> queryable,
            Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default)
            => Task.FromResult(queryable.Any(predicate));

        public Task<T> SingleAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(queryable.Single());
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
        }

        public List<TEntity> Items => _items;

        public Task<IQueryable<TEntity>> GetQueryableAsync(bool includeDetails = true)
            => Task.FromResult(_items.AsQueryable());

        public Task DeleteAsync(Guid id, bool autoSave = false, CancellationToken cancellationToken = default)
        {
            var entity = _items.FirstOrDefault(x => _keySelector(x) == id);
            if (entity != null)
            {
                _items.Remove(entity);
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default)
        {
            _items.Remove(entity);
            return Task.CompletedTask;
        }

        public Task<TEntity> InsertAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default)
        {
            if (!_items.Contains(entity))
            {
                _items.Add(entity);
            }

            return Task.FromResult(entity);
        }

        public Task<TEntity> UpdateAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default)
        {
            var id = _keySelector(entity);
            var existing = _items.FirstOrDefault(x => _keySelector(x).Equals(id));
            if (existing != null)
            {
                _items.Remove(existing);
            }

            _items.Add(entity);
            return Task.FromResult(entity);
        }

        public Task<TEntity> GetAsync(Guid id, bool includeDetails = true, CancellationToken cancellationToken = default)
        {
            var entity = _items.FirstOrDefault(x => _keySelector(x) == id);
            if (entity == null)
            {
                throw new KeyNotFoundException($"Entity {typeof(TEntity).Name} with id {id} was not found");
            }

            return Task.FromResult(entity);
        }

        public Task<TEntity?> FindAsync(Guid id, bool includeDetails = true, CancellationToken cancellationToken = default)
        {
            var entity = _items.FirstOrDefault(x => _keySelector(x) == id);
            return Task.FromResult(entity);
        }

        public Task<long> GetCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult((long)_items.Count);

        public Task<List<TEntity>> GetListAsync(bool includeDetails = false, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.ToList());

        public Task<List<TEntity>> GetPagedListAsync(
            int skipCount,
            int maxResultCount,
            string sorting,
            bool includeDetails = false,
            CancellationToken cancellationToken = default)
        {
            var query = _items.Skip(skipCount).Take(maxResultCount).ToList();
            return Task.FromResult(query);
        }

        public Task<List<TEntity>> GetListAsync(
            Expression<Func<TEntity, bool>> predicate,
            bool includeDetails = false,
            CancellationToken cancellationToken = default)
        {
            var compiled = predicate.Compile();
            return Task.FromResult(_items.Where(compiled).ToList());
        }

        public Task DeleteAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
        {
            var compiled = predicate.Compile();
            _items.RemoveAll(x => compiled(x));
            return Task.CompletedTask;
        }

        public Task<long> GetCountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
        {
            var compiled = predicate.Compile();
            return Task.FromResult((long)_items.Count(compiled));
        }

        public Task<TEntity> SingleAsync(
            Expression<Func<TEntity, bool>> predicate,
            bool includeDetails = false,
            CancellationToken cancellationToken = default)
        {
            var compiled = predicate.Compile();
            return Task.FromResult(_items.Single(compiled));
        }

        public Task<TEntity?> FirstOrDefaultAsync(
            Expression<Func<TEntity, bool>> predicate,
            bool includeDetails = false,
            CancellationToken cancellationToken = default)
        {
            var compiled = predicate.Compile();
            return Task.FromResult(_items.FirstOrDefault(compiled));
        }

        public Task<List<TEntity>> InsertManyAsync(
            IEnumerable<TEntity> entities,
            bool autoSave = false,
            CancellationToken cancellationToken = default)
        {
            var list = entities.ToList();
            foreach (var entity in list)
            {
                if (!_items.Contains(entity))
                {
                    _items.Add(entity);
                }
            }

            return Task.FromResult(list);
        }

        public Task<List<TEntity>> UpdateManyAsync(
            IEnumerable<TEntity> entities,
            bool autoSave = false,
            CancellationToken cancellationToken = default)
        {
            var list = entities.ToList();
            foreach (var entity in list)
            {
                var id = _keySelector(entity);
                var existing = _items.FirstOrDefault(x => _keySelector(x).Equals(id));
                if (existing != null)
                {
                    _items.Remove(existing);
                }

                _items.Add(entity);
            }

            return Task.FromResult(list);
        }

        public Task DeleteManyAsync(
            IEnumerable<TEntity> entities,
            bool autoSave = false,
            CancellationToken cancellationToken = default)
        {
            foreach (var entity in entities)
            {
                _items.Remove(entity);
            }

            return Task.CompletedTask;
        }

        public Task DeleteManyAsync(
            IEnumerable<Guid> ids,
            bool autoSave = false,
            CancellationToken cancellationToken = default)
        {
            var set = new HashSet<Guid>(ids);
            _items.RemoveAll(x => set.Contains(_keySelector(x)));
            return Task.CompletedTask;
        }

        public Task<TEntity?> FindAsync(
            Expression<Func<TEntity, bool>> predicate,
            bool includeDetails = false,
            CancellationToken cancellationToken = default)
        {
            var compiled = predicate.Compile();
            return Task.FromResult(_items.FirstOrDefault(compiled));
        }

        public Task<IQueryable<TEntity>> WithDetailsAsync(Expression<Func<TEntity, object>>[] propertySelectors)
            => Task.FromResult(_items.AsQueryable());

        public Task<IQueryable<TEntity>> WithDetailsAsync()
            => Task.FromResult(_items.AsQueryable());

        public IQueryable<TEntity> AsQueryable() => _items.AsQueryable();

        public IQueryable<TEntity> WithDetails() => _items.AsQueryable();

        public TEntity Attach(TEntity entity) => entity;

        public void Detach(TEntity entity)
        {
        }

        public Task EnsureCollectionLoadedAsync<TProperty>(
            TEntity entity,
            Expression<Func<TEntity, IEnumerable<TProperty>>> propertyExpression,
            CancellationToken cancellationToken = default)
            where TProperty : class
        {
            return Task.CompletedTask;
        }

        public Task EnsurePropertyLoadedAsync<TProperty>(
            TEntity entity,
            Expression<Func<TEntity, TProperty>> propertyExpression,
            CancellationToken cancellationToken = default)
            where TProperty : class
        {
            return Task.CompletedTask;
        }

        public Task<TEntity> InsertAsync(
            TEntity entity,
            bool autoSave,
            CancellationToken cancellationToken,
            bool? autoSaveChanges)
        {
            return InsertAsync(entity, autoSave, cancellationToken);
        }

        public Task<List<TEntity>> InsertManyAsync(
            IEnumerable<TEntity> entities,
            bool autoSave,
            CancellationToken cancellationToken,
            bool? autoSaveChanges)
        {
            return InsertManyAsync(entities, autoSave, cancellationToken);
        }

        public Task<TEntity> UpdateAsync(
            TEntity entity,
            bool autoSave,
            CancellationToken cancellationToken,
            bool? autoSaveChanges)
        {
            return UpdateAsync(entity, autoSave, cancellationToken);
        }

        public Task<List<TEntity>> UpdateManyAsync(
            IEnumerable<TEntity> entities,
            bool autoSave,
            CancellationToken cancellationToken,
            bool? autoSaveChanges)
        {
            return UpdateManyAsync(entities, autoSave, cancellationToken);
        }

        public Task DeleteAsync(
            TEntity entity,
            bool autoSave,
            CancellationToken cancellationToken,
            bool? autoSaveChanges)
        {
            return DeleteAsync(entity, autoSave, cancellationToken);
        }

        public Task DeleteManyAsync(
            IEnumerable<TEntity> entities,
            bool autoSave,
            CancellationToken cancellationToken,
            bool? autoSaveChanges)
        {
            return DeleteManyAsync(entities, autoSave, cancellationToken);
        }

        public Task DeleteManyAsync(
            IEnumerable<Guid> ids,
            bool autoSave,
            CancellationToken cancellationToken,
            bool? autoSaveChanges)
        {
            return DeleteManyAsync(ids, autoSave, cancellationToken);
        }

        public Task<List<TEntity>> GetListAsync(
            Expression<Func<TEntity, bool>> predicate,
            bool includeDetails,
            CancellationToken cancellationToken,
            bool? autoSaveChanges)
        {
            return GetListAsync(predicate, includeDetails, cancellationToken);
        }
    }
}
