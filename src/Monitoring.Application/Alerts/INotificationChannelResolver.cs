namespace Monitoring.Alerts;

public interface INotificationChannelResolver
{
    INotificationChannel Resolve(string channel);
}
