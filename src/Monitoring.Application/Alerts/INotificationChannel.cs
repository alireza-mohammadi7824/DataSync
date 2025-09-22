using System.Threading;
using System.Threading.Tasks;

namespace Monitoring.Alerts;

public interface INotificationChannel
{
    string Name { get; }

    Task SendAsync(AlertDispatch dispatch, CancellationToken ct = default);
}
