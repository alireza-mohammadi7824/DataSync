using System;
using Monitoring.Alerts;
using Monitoring.Targets;
using Xunit;

namespace HRSDataIntegration.Monitoring;

public class AlertEvaluatorTests
{
    [Fact]
    public void Down_alert_triggers_after_threshold()
    {
        var targetId = Guid.NewGuid();
        var outage = new OutageWindow(Guid.NewGuid(), targetId, DateTime.UtcNow.AddMinutes(-5), 1);

        var inputBelow = new AlertEvaluationInput(
            previousStatus: ServiceStatus.Online,
            currentStatus: ServiceStatus.Offline,
            consecutiveFailures: 2,
            timestamp: DateTime.UtcNow,
            notifyAfterFailures: 3,
            repeatMinutes: 60,
            recoverQuietMinutes: 10,
            enabled: true,
            underMaintenance: false,
            activeOutage: outage,
            closedOutage: null,
            previousLastUpAt: DateTime.UtcNow.AddMinutes(-30));

        var resultBelow = AlertEvaluator.Evaluate(inputBelow);
        Assert.Null(resultBelow.EventType);

        var inputThreshold = new AlertEvaluationInput(
            previousStatus: ServiceStatus.Online,
            currentStatus: ServiceStatus.Offline,
            consecutiveFailures: 3,
            timestamp: DateTime.UtcNow,
            notifyAfterFailures: 3,
            repeatMinutes: 60,
            recoverQuietMinutes: 10,
            enabled: true,
            underMaintenance: false,
            activeOutage: outage,
            closedOutage: null,
            previousLastUpAt: DateTime.UtcNow.AddMinutes(-30));

        var resultThreshold = AlertEvaluator.Evaluate(inputThreshold);
        Assert.Equal(AlertEventType.Down, resultThreshold.EventType);
        Assert.True(resultThreshold.ShouldRecordAlert);
    }

    [Fact]
    public void Still_down_repeats_after_interval()
    {
        var targetId = Guid.NewGuid();
        var outage = new OutageWindow(Guid.NewGuid(), targetId, DateTime.UtcNow.AddHours(-2), 1);
        outage.RecordAlert(DateTime.UtcNow.AddMinutes(-30));

        var inputTooSoon = new AlertEvaluationInput(
            previousStatus: ServiceStatus.Offline,
            currentStatus: ServiceStatus.Offline,
            consecutiveFailures: 5,
            timestamp: DateTime.UtcNow.AddMinutes(-10),
            notifyAfterFailures: 1,
            repeatMinutes: 30,
            recoverQuietMinutes: 10,
            enabled: true,
            underMaintenance: false,
            activeOutage: outage,
            closedOutage: null,
            previousLastUpAt: DateTime.UtcNow.AddHours(-3));

        var resultTooSoon = AlertEvaluator.Evaluate(inputTooSoon);
        Assert.Null(resultTooSoon.EventType);

        var inputDue = new AlertEvaluationInput(
            previousStatus: ServiceStatus.Offline,
            currentStatus: ServiceStatus.Offline,
            consecutiveFailures: 6,
            timestamp: DateTime.UtcNow,
            notifyAfterFailures: 1,
            repeatMinutes: 30,
            recoverQuietMinutes: 10,
            enabled: true,
            underMaintenance: false,
            activeOutage: outage,
            closedOutage: null,
            previousLastUpAt: DateTime.UtcNow.AddHours(-3));

        var resultDue = AlertEvaluator.Evaluate(inputDue);
        Assert.Equal(AlertEventType.StillDown, resultDue.EventType);
        Assert.True(resultDue.ShouldRecordAlert);
    }

    [Fact]
    public void Recovered_alert_respects_quiet_minutes()
    {
        var outage = new OutageWindow(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddHours(-1), 1);
        outage.RecordAlert(DateTime.UtcNow.AddMinutes(-30));

        var inputTooSoon = new AlertEvaluationInput(
            previousStatus: ServiceStatus.Offline,
            currentStatus: ServiceStatus.Online,
            consecutiveFailures: 0,
            timestamp: DateTime.UtcNow,
            notifyAfterFailures: 1,
            repeatMinutes: 60,
            recoverQuietMinutes: 30,
            enabled: true,
            underMaintenance: false,
            activeOutage: null,
            closedOutage: outage,
            previousLastUpAt: DateTime.UtcNow.AddMinutes(-15));

        var resultTooSoon = AlertEvaluator.Evaluate(inputTooSoon);
        Assert.Null(resultTooSoon.EventType);

        var inputQuietMet = new AlertEvaluationInput(
            previousStatus: ServiceStatus.Offline,
            currentStatus: ServiceStatus.Online,
            consecutiveFailures: 0,
            timestamp: DateTime.UtcNow,
            notifyAfterFailures: 1,
            repeatMinutes: 60,
            recoverQuietMinutes: 30,
            enabled: true,
            underMaintenance: false,
            activeOutage: null,
            closedOutage: outage,
            previousLastUpAt: DateTime.UtcNow.AddMinutes(-45));

        var resultQuietMet = AlertEvaluator.Evaluate(inputQuietMet);
        Assert.Equal(AlertEventType.Recovered, resultQuietMet.EventType);
        Assert.False(resultQuietMet.ShouldRecordAlert);
    }

    [Fact]
    public void Maintenance_suppresses_alerts()
    {
        var outage = new OutageWindow(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-5), 1);

        var input = new AlertEvaluationInput(
            previousStatus: ServiceStatus.Online,
            currentStatus: ServiceStatus.Offline,
            consecutiveFailures: 5,
            timestamp: DateTime.UtcNow,
            notifyAfterFailures: 1,
            repeatMinutes: 60,
            recoverQuietMinutes: 10,
            enabled: true,
            underMaintenance: true,
            activeOutage: outage,
            closedOutage: null,
            previousLastUpAt: DateTime.UtcNow.AddHours(-1));

        var result = AlertEvaluator.Evaluate(input);
        Assert.Null(result.EventType);
    }
}
