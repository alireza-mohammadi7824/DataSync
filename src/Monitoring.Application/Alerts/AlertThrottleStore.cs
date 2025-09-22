using System;
using System.Collections.Concurrent;

namespace Monitoring.Alerts;

public sealed class AlertThrottleStore
{
    private readonly ConcurrentDictionary<(Guid TargetId, string EventType), DateTime> _lastSent
        = new();

    public bool ShouldThrottle(Guid targetId, string eventType, int cooldownSeconds, DateTime nowUtc, out TimeSpan remaining)
    {
        remaining = TimeSpan.Zero;
        if (cooldownSeconds <= 0)
        {
            return false;
        }

        var key = (targetId, eventType);
        if (!_lastSent.TryGetValue(key, out var last))
        {
            return false;
        }

        var elapsed = nowUtc - last;
        var cooldown = TimeSpan.FromSeconds(cooldownSeconds);
        if (elapsed >= cooldown)
        {
            return false;
        }

        remaining = cooldown - elapsed;
        return true;
    }

    public void MarkSent(Guid targetId, string eventType, DateTime nowUtc)
    {
        var key = (targetId, eventType);
        _lastSent.AddOrUpdate(key, nowUtc, (_, _) => nowUtc);
    }
}
