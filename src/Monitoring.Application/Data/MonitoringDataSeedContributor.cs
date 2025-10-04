using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Monitoring.Targets;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;

namespace Monitoring.Data;

public sealed class MonitoringDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private static readonly Guid WebsiteTargetId = Guid.Parse("6CE5A7F1-14B5-4D53-9E0E-4C03E9C5F0D1");
    private static readonly Guid TcpTargetId = Guid.Parse("1545D7B3-7AC2-4F2D-9E3C-7A3B6B2AFA21");
    private static readonly Guid SamplePolicyId = Guid.Parse("B6D1954E-5D60-4FF3-A00B-053FACAC0E1E");

    private readonly IRepository<MonitoringTarget, Guid> _targetRepository;
    private readonly IRepository<AlertPolicy, Guid> _alertPolicyRepository;
    private readonly IClock _clock;

    public MonitoringDataSeedContributor(
        IRepository<MonitoringTarget, Guid> targetRepository,
        IRepository<AlertPolicy, Guid> alertPolicyRepository,
        IClock clock)
    {
        _targetRepository = targetRepository;
        _alertPolicyRepository = alertPolicyRepository;
        _clock = clock;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        var cancellationToken = context?.CancellationToken ?? default;

        var websiteId = await EnsureWebsiteTargetAsync(cancellationToken);
        await EnsureTcpTargetAsync(cancellationToken);
        await EnsureSamplePolicyAsync(websiteId, cancellationToken);
    }

    private async Task<Guid> EnsureWebsiteTargetAsync(CancellationToken cancellationToken)
    {
        var existing = await _targetRepository.FindAsync(WebsiteTargetId, cancellationToken: cancellationToken);
        if (existing != null)
        {
            return existing.Id;
        }

        var nowUtc = _clock.Now.ToUniversalTime();
        var target = new MonitoringTarget(
            WebsiteTargetId,
            "Sample Website (disabled)",
            ServiceType.Website,
            "https://example.com",
            checkIntervalSeconds: 300,
            timeoutSeconds: 30,
            maxRetryAttempts: 1,
            retryDelaySeconds: 5,
            isActive: false,
            currentStatus: ServiceStatus.Online,
            nextDueAt: nowUtc.AddMinutes(5),
            settingsJson: null,
            category: "Samples");

        target.SetLastCheckedAt(null);
        target.SetLastStatusChangeAt(null);
        target.SetFirstDownAt(null);
        target.SetLastUpAt(null);
        target.SetConsecutiveFailures(0);

        await _targetRepository.InsertAsync(target, autoSave: true, cancellationToken: cancellationToken);
        return target.Id;
    }

    private async Task EnsureTcpTargetAsync(CancellationToken cancellationToken)
    {
        var existing = await _targetRepository.FindAsync(TcpTargetId, cancellationToken: cancellationToken);
        if (existing != null)
        {
            return;
        }

        var nowUtc = _clock.Now.ToUniversalTime();
        var target = new MonitoringTarget(
            TcpTargetId,
            "Sample TCP Endpoint (disabled)",
            ServiceType.Tcp,
            "127.0.0.1:80",
            checkIntervalSeconds: 300,
            timeoutSeconds: 10,
            maxRetryAttempts: 0,
            retryDelaySeconds: 5,
            isActive: false,
            currentStatus: ServiceStatus.Online,
            nextDueAt: nowUtc.AddMinutes(5),
            settingsJson: null,
            category: "Samples");

        target.SetLastCheckedAt(null);
        target.SetLastStatusChangeAt(null);
        target.SetFirstDownAt(null);
        target.SetLastUpAt(null);
        target.SetConsecutiveFailures(0);

        await _targetRepository.InsertAsync(target, autoSave: true, cancellationToken: cancellationToken);
    }

    private async Task EnsureSamplePolicyAsync(Guid websiteTargetId, CancellationToken cancellationToken)
    {
        var existingById = await _alertPolicyRepository.FindAsync(SamplePolicyId, cancellationToken: cancellationToken);
        if (existingById != null)
        {
            return;
        }

        var existingByTarget = await _alertPolicyRepository.FirstOrDefaultAsync(x => x.TargetId == websiteTargetId, cancellationToken: cancellationToken);
        if (existingByTarget != null)
        {
            return;
        }

        var payload = new
        {
            onDown = true,
            onUp = true,
            minDownDurationSeconds = 60,
            cooldownSeconds = 300,
            emails = Array.Empty<string>(),
            webhookUrl = (string?)null
        };

        var channelsJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var policy = new AlertPolicy(
            SamplePolicyId,
            websiteTargetId,
            enabled: true,
            notifyAfterFailures: 1,
            repeatMinutes: 5,
            recoverQuietMinutes: 1,
            channelsJson: channelsJson,
            suppressDuringMaintenance: true);

        await _alertPolicyRepository.InsertAsync(policy, autoSave: true, cancellationToken: cancellationToken);
    }
}
