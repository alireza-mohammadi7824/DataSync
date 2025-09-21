using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Monitoring.Alerts;
using Monitoring.Targets;
using Xunit;

namespace HRSDataIntegration.Monitoring;

public class NotificationChannelTests
{
    [Fact]
    public async Task Webhook_channel_posts_expected_payload()
    {
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        var builder = new WebHostBuilder()
            .ConfigureServices(_ => { })
            .Configure(app =>
            {
                app.Run(async context =>
                {
                    using var reader = new System.IO.StreamReader(context.Request.Body);
                    var body = await reader.ReadToEndAsync();
                    tcs.TrySetResult(body);
                    context.Response.StatusCode = 200;
                    await context.Response.CompleteAsync();
                });
            });

        using var server = new TestServer(builder);
        using var client = server.CreateClient();
        var factory = new TestHttpClientFactory(client);

        var channel = new WebhookNotificationChannel(
            factory,
            NullLoggerFactory.Instance.CreateLogger<WebhookNotificationChannel>(),
            new List<string> { server.BaseAddress.ToString() },
            new Dictionary<string, string>());

        var snapshot = new TargetSnapshot(
            Guid.NewGuid(),
            "Web API",
            ServiceType.Api,
            "https://api.example.com/health",
            ServiceStatus.Offline,
            DateTime.UtcNow,
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(-5),
            null,
            null);

        var payload = new AlertPayload(
            AlertEventType.Down,
            DateTime.UtcNow,
            "Timeout",
            123,
            null);

        await channel.SendAsync(snapshot, payload, CancellationToken.None);

        var body = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.Equal("Down", root.GetProperty("eventType").GetString());
        var target = root.GetProperty("target");
        Assert.Equal(snapshot.TargetId.ToString(), target.GetProperty("id").GetString());
        Assert.Equal("Offline", target.GetProperty("status").GetString());
        var details = root.GetProperty("details");
        Assert.Equal("Timeout", details.GetProperty("errorSummary").GetString());
        Assert.Equal(123, details.GetProperty("responseTimeMs").GetInt32());
    }

    [Fact]
    public async Task Telegram_channel_skips_without_configuration()
    {
        var factory = new TestHttpClientFactory(new HttpClient(new HttpClientHandler()));
        var channel = new TelegramNotificationChannel(
            factory,
            NullLoggerFactory.Instance.CreateLogger<TelegramNotificationChannel>(),
            string.Empty,
            Array.Empty<string>());

        var snapshot = new TargetSnapshot(
            Guid.NewGuid(),
            "Redis",
            ServiceType.Redis,
            "redis:6379",
            ServiceStatus.Online,
            DateTime.UtcNow,
            DateTime.UtcNow,
            null,
            DateTime.UtcNow,
            null);

        var payload = new AlertPayload(AlertEventType.Recovered, DateTime.UtcNow, null, null, null);

        await channel.SendAsync(snapshot, payload, CancellationToken.None);
    }

    private sealed class TestHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public TestHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name)
        {
            return _client;
        }
    }
}
