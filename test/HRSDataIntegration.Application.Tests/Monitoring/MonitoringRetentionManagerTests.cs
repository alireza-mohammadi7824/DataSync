using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monitoring.Execution;
using Monitoring.Options;
using Monitoring.Targets;
using Shouldly;
using Volo.Abp.Domain.Repositories;
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
    }

    private sealed class FakeAsyncQueryableExecuter : IAsyncQueryableExecuter
    {
        public Task<List<T>> ToListAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(queryable.ToList());

        public Task<T> FirstOrDefaultAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(queryable.FirstOrDefault());

        public Task<long> LongCountAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(queryable.LongCount());

        public Task<bool> AnyAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(queryable.Any());

        public Task<T> SingleAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
            => Task.FromResult(queryable.Single());
    }

    private sealed class FakeRepository<TEntity> : IRepository<TEntity, Guid> where TEntity : class
    {
        private readonly List<TEntity> _items;
        private readonly Func<TEntity, Guid> _keySelector;

        public FakeRepository(IEnumerable<TEntity> items, Func<TEntity, Guid> keySelector)
        {
            _items = items.ToList();
            _keySelector = keySelector;
        }

        public List<TEntity> Items => _items;

        public Task<IQueryable<TEntity>> GetQueryableAsync(bool includeDetails = true) => Task.FromResult(_items.AsQueryable());

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

        #region Not Implemented Members

        public Task<TEntity> InsertAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TEntity> UpdateAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TEntity> GetAsync(Guid id, bool includeDetails = true, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TEntity> FindAsync(Guid id, bool includeDetails = true, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<long> GetCountAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<TEntity>> GetListAsync(bool includeDetails = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<TEntity>> GetPagedListAsync(int skipCount, int maxResultCount, string sorting, bool includeDetails = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<TEntity>> GetListAsync(System.Linq.Expressions.Expression<Func<TEntity, bool>> predicate, bool includeDetails = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeleteAsync(System.Linq.Expressions.Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<long> GetCountAsync(System.Linq.Expressions.Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TEntity> SingleAsync(System.Linq.Expressions.Expression<Func<TEntity, bool>> predicate, bool includeDetails = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TEntity> FirstOrDefaultAsync(System.Linq.Expressions.Expression<Func<TEntity, bool>> predicate, bool includeDetails = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<TEntity>> InsertManyAsync(IEnumerable<TEntity> entities, bool autoSave = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<TEntity>> UpdateManyAsync(IEnumerable<TEntity> entities, bool autoSave = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeleteManyAsync(IEnumerable<TEntity> entities, bool autoSave = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeleteManyAsync(IEnumerable<Guid> ids, bool autoSave = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TEntity> FindAsync(System.Linq.Expressions.Expression<Func<TEntity, bool>> predicate, bool includeDetails = false, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IQueryable<TEntity>> WithDetailsAsync(System.Linq.Expressions.Expression<Func<TEntity, object>>[] propertySelectors) => throw new NotImplementedException();
        public Task<IQueryable<TEntity>> WithDetailsAsync() => throw new NotImplementedException();
        public IQueryable<TEntity> AsQueryable() => _items.AsQueryable();
        public IQueryable<TEntity> WithDetails() => throw new NotImplementedException();
        public TEntity Attach(TEntity entity) => throw new NotImplementedException();
        public void Detach(TEntity entity) => throw new NotImplementedException();
        public Task EnsureCollectionLoadedAsync<TProperty>(TEntity entity, System.Linq.Expressions.Expression<Func<TEntity, IEnumerable<TProperty>>> propertyExpression, CancellationToken cancellationToken = default) where TProperty : class => throw new NotImplementedException();
        public Task EnsurePropertyLoadedAsync<TProperty>(TEntity entity, System.Linq.Expressions.Expression<Func<TEntity, TProperty>> propertyExpression, CancellationToken cancellationToken = default) where TProperty : class => throw new NotImplementedException();
        public Task<TEntity> InsertAsync(TEntity entity, bool autoSave, CancellationToken cancellationToken, bool? autoSaveChanges) => throw new NotImplementedException();
        public Task<List<TEntity>> InsertManyAsync(IEnumerable<TEntity> entities, bool autoSave, CancellationToken cancellationToken, bool? autoSaveChanges) => throw new NotImplementedException();
        public Task<TEntity> UpdateAsync(TEntity entity, bool autoSave, CancellationToken cancellationToken, bool? autoSaveChanges) => throw new NotImplementedException();
        public Task<List<TEntity>> UpdateManyAsync(IEnumerable<TEntity> entities, bool autoSave, CancellationToken cancellationToken, bool? autoSaveChanges) => throw new NotImplementedException();
        public Task DeleteAsync(TEntity entity, bool autoSave, CancellationToken cancellationToken, bool? autoSaveChanges) => throw new NotImplementedException();
        public Task DeleteManyAsync(IEnumerable<TEntity> entities, bool autoSave, CancellationToken cancellationToken, bool? autoSaveChanges) => throw new NotImplementedException();
        public Task DeleteManyAsync(IEnumerable<Guid> ids, bool autoSave, CancellationToken cancellationToken, bool? autoSaveChanges) => throw new NotImplementedException();
        public Task<List<TEntity>> GetListAsync(System.Linq.Expressions.Expression<Func<TEntity, bool>> predicate, bool includeDetails, CancellationToken cancellationToken, bool? autoSaveChanges) => throw new NotImplementedException();
        #endregion
    }
}
