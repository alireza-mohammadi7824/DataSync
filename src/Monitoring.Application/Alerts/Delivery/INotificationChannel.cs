using System.Threading;
using System.Threading.Tasks;
using Monitoring.Alerts;

namespace Monitoring.Alerts.Delivery;

public interface INotificationChannel
{
    NotificationChannelType Type { get; }

    Task SendAsync(AlertNotificationDto dto, CancellationToken cancellationToken);
}
