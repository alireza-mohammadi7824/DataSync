using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Monitoring.Execution;

public sealed class BulkCheckProcessor : BackgroundService
{
    private readonly IBulkCheckQueue _queue;

    public BulkCheckProcessor(IBulkCheckQueue queue)
    {
        _queue = queue;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return _queue.StartAsync(stoppingToken);
    }
}
