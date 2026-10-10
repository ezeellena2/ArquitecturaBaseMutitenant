using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.WhatsAppBrowser;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

public sealed class WhatsAppFileBridgeTests
{
    [Fact]
    public async Task A_pending_signed_webhook_command_rejects_a_second_concurrent_order()
    {
        using var services = Services("Testing");
        var directory = Path.Combine(Path.GetTempPath(), "mt-e2e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var ready = Path.Combine(directory, "mt-e2e-pending.ready");
            var command = Path.ChangeExtension(ready, ".whatsapp.json");
            var bridge = new WhatsAppFileBridge(services, ready, isolated: true);
            using var handler = new PendingWebhookHandler();
            using var client = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };
            await File.WriteAllTextAsync(command, "{\"kind\":\"begin\",\"scenario\":\"walkthrough-pending\",\"phone\":\"+5491134567880\"}", TestContext.Current.CancellationToken);
            Assert.True(await bridge.TryExecuteAsync(client, TestContext.Current.CancellationToken));
            File.Delete(command + ".done");
            await File.WriteAllTextAsync(command, "{\"kind\":\"inbound\",\"scenario\":\"walkthrough-pending\",\"phone\":\"+5491134567880\"}", TestContext.Current.CancellationToken);
            var pending = bridge.TryExecuteAsync(client, TestContext.Current.CancellationToken);
            await handler.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<InvalidOperationException>(() => bridge.TryExecuteAsync(client, TestContext.Current.CancellationToken));
            handler.Release.SetResult();
            Assert.True(await pending);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task Lower_camel_command_is_executed_once_and_response_is_published_atomically()
    {
        using var services = Services("Testing");
        var directory = Path.Combine(Path.GetTempPath(), "mt-e2e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var ready = Path.Combine(directory, "mt-e2e-test.ready");
            var command = Path.ChangeExtension(ready, ".whatsapp.json");
            var bridge = new WhatsAppFileBridge(services, ready, isolated: true);
            using var client = new HttpClient();
            Assert.False(await bridge.TryExecuteAsync(client, TestContext.Current.CancellationToken));
            await File.WriteAllTextAsync(command, "{\"kind\":\"begin\",\"scenario\":\"walkthrough-test\",\"phone\":\"+5491134567880\"}", TestContext.Current.CancellationToken);
            Assert.True(await bridge.TryExecuteAsync(client, TestContext.Current.CancellationToken));
            using var result = JsonDocument.Parse(await File.ReadAllTextAsync(command + ".done", TestContext.Current.CancellationToken));
            Assert.True(result.RootElement.GetProperty("ok").GetBoolean());
            Assert.False(File.Exists(command + ".done.tmp"));
            Assert.False(await bridge.TryExecuteAsync(client, TestContext.Current.CancellationToken));
            File.Delete(command + ".done");
            await File.WriteAllTextAsync(command, "{\"kind\":\"code\",\"scenario\":\"unknown\",\"phone\":\"+5491134567880\"}", TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<InvalidOperationException>(() => bridge.TryExecuteAsync(client, TestContext.Current.CancellationToken));
            await File.WriteAllTextAsync(command, new string('x', 2049), TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<InvalidOperationException>(() => bridge.TryExecuteAsync(client, TestContext.Current.CancellationToken));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Theory]
    [InlineData("Development", true)]
    [InlineData("Production", true)]
    [InlineData("Testing", false)]
    public void Bridge_requires_testing_and_explicit_isolation(string environment, bool isolated)
    {
        using var services = Services(environment);
        Assert.Throws<InvalidOperationException>(() => new WhatsAppFileBridge(services,
            Path.Combine(Path.GetTempPath(), "mt-e2e-test", "mt-e2e-test.ready"), isolated));
    }

    [Theory]
    [InlineData("relative.ready")]
    [InlineData("C:/mt-e2e-test.ready")]
    public void Bridge_rejects_paths_outside_the_owned_temporary_directory(string path)
    {
        using var services = Services("Testing");
        Assert.Throws<InvalidOperationException>(() => new WhatsAppFileBridge(services, path, isolated: true));
    }

    private static ServiceProvider Services(string environment) => new ServiceCollection()
        .AddSingleton<IHostEnvironment>(new BridgeEnvironment { EnvironmentName = environment })
        .AddSingleton(TimeProvider.System).BuildServiceProvider();

    private sealed class BridgeEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "Fixture";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class PendingWebhookHandler : HttpMessageHandler
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Started.SetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        }
    }
}
