using System;
using System.Linq;
using System.Threading.Tasks;
using Monitoring.Execution;
using Shouldly;
using Volo.Abp.Guids;
using Xunit;

namespace HRSDataIntegration.Application.Tests.Monitoring;

public class BulkCheckQueueTests
{
    [Fact]
    public async Task Enqueue_ShouldTrackStatus()
    {
        var guidGenerator = new DeterministicGuidGenerator();
        var queue = new BulkCheckQueue(guidGenerator);
        var targetIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        var result = await queue.EnqueueAsync(targetIds);

        result.TotalQueued.ShouldBe(3);

        var status = queue.GetStatus(result.BatchId);
        status.TotalTargets.ShouldBe(3);
        status.Queued.ShouldBe(3);
        status.Completed.ShouldBe(0);

        queue.TryBegin(result.BatchId).ShouldBeTrue();
        queue.Complete(result.BatchId, success: true, skipped: false);

        status = queue.GetStatus(result.BatchId);
        status.Running.ShouldBe(0);
        status.Completed.ShouldBe(1);
        status.Succeeded.ShouldBe(1);
        status.Queued.ShouldBe(2);
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
