using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monitoring.Execution;
using Monitoring.Options;
using Shouldly;
using Volo.Abp.Guids;
using Xunit;

namespace HRSDataIntegration.Application.Tests.Monitoring;

public class BulkCheckQueueTests
{
    [Fact]
    public void Enqueue_ShouldInitializeBatchStatus()
    {
        var services = new ServiceCollection();
        using var provider = services.BuildServiceProvider();

        var queue = new BulkCheckQueue(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new DeterministicGuidGenerator(),
            Options.Create(new MonitoringOptions()),
            NullLogger<BulkCheckQueue>.Instance);

        var targetIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        var batchId = queue.Enqueue(targetIds);

        var status = queue.GetStatus(batchId);
        status.BatchId.ShouldBe(batchId);
        status.Total.ShouldBe(3);
        status.Queued.ShouldBe(3);
        status.Running.ShouldBe(0);
        status.Completed.ShouldBe(0);
        status.Failed.ShouldBe(0);
    }

    private sealed class DeterministicGuidGenerator : IGuidGenerator
    {
        private int _counter;

        public Guid Create()
        {
            _counter++;
            var bytes = new byte[16];
            BitConverter.GetBytes(_counter).CopyTo(bytes, 0);
            return new Guid(bytes);
        }

        public Guid CreateSequential() => Create();
    }
}
