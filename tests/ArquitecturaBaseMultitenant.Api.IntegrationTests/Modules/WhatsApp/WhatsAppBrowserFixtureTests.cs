using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.WhatsAppBrowser;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

/// <summary>El canal privado de control sólo acepta órdenes acotadas a escenarios y destinos sintéticos.</summary>
public sealed class WhatsAppBrowserFixtureTests
{
    [Theory]
    [InlineData("code", "signup-1440", "+5491134567880", true)]
    [InlineData("begin", "operator", "+5491134567899", true)]
    [InlineData("events", "recovery-390", "+5491134567881", true)]
    [InlineData("operatorCode", "platform", "+5491134567899", true)]
    [InlineData("sql", "signup", "+5491134567880", false)]
    [InlineData("code", "", "+5491134567880", false)]
    [InlineData("code", "../other", "+5491134567880", false)]
    [InlineData("code", "signup", "+5491199999999", false)]
    [InlineData("code", "signup", "+54911345678800", false)]
    [InlineData("code", "signup", "+549113456788x", false)]
    public void Control_rejects_unknown_operations_and_non_fixture_destinations(string kind, string scenario, string phone, bool expected) =>
        Assert.Equal(expected, new WhatsAppBrowserRequest(kind, scenario, phone).IsValid());

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void Control_is_unavailable_outside_testing(string environment)
    {
        using var services = Services(environment);
        Assert.Throws<InvalidOperationException>(() => new WhatsAppBrowserControl(services));
    }

    [Fact]
    public async Task A_scenario_cannot_read_another_destination_or_replace_its_cursor()
    {
        using var services = Services("Testing");
        using var client = new HttpClient();
        var control = new WhatsAppBrowserControl(services);
        var request = new WhatsAppBrowserRequest("begin", "signup", "+5491134567880");
        await control.ExecuteAsync(request, client, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => control.ExecuteAsync(request, client, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => control.ExecuteAsync(request with { Kind = "code", Phone = "+5491134567881" }, client, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => control.ExecuteAsync(request with { Kind = "code", Scenario = "other" }, client, TestContext.Current.CancellationToken));
    }

    private static ServiceProvider Services(string environment) => new ServiceCollection()
        .AddSingleton<IHostEnvironment>(new FixtureEnvironment { EnvironmentName = environment })
        .AddSingleton(TimeProvider.System).BuildServiceProvider();

    [Theory]
    [InlineData("1440")]
    [InlineData("390")]
    public void Private_commands_require_exact_identifiers_and_backup_email_scenario(string width)
    {
        var recovery = JsonSerializer.Deserialize<WhatsAppBrowserRequest>(
            "{\"Kind\":\"recoveryNotice\",\"Scenario\":\"recovered-1440\",\"Phone\":\"+5491134567880\",\"RecoveryId\":\"9e9ed364-3d8f-48c7-a126-f07bbbc31537\"}")!;
        Assert.True(recovery.IsValid());
        var email = JsonSerializer.Deserialize<WhatsAppBrowserRequest>(
            "{\"Kind\":\"methodEmailCode\",\"Scenario\":\"backup-email-" + width + "\",\"Phone\":\"+5491134567880\",\"Email\":\"e8-backup-" + width + "@example.test\",\"MethodId\":\"9e9ed364-3d8f-48c7-a126-f07bbbc31537\"}")!;
        Assert.True(email.IsValid());
        Assert.Equal(nameof(WhatsAppBrowserRequest), email.ToString());
    }

    [Fact]
    public void Private_commands_reject_missing_empty_or_non_matching_identifiers()
    {
        var id = Guid.NewGuid();
        var recovery = new WhatsAppBrowserRequest("recoveryNotice", "recovered-1440", "+5491134567880", RecoveryId: id);
        Assert.False((recovery with { RecoveryId = null }).IsValid());
        Assert.False((recovery with { RecoveryId = Guid.Empty }).IsValid());
        var email = new WhatsAppBrowserRequest("methodEmailCode", "backup-email-1440", recovery.Phone,
            Email: "e8-backup-1440@example.test", MethodId: id);
        Assert.True(email.IsValid());
        Assert.False((email with { MethodId = null }).IsValid());
        Assert.False((email with { MethodId = Guid.Empty }).IsValid());
        Assert.False((email with { Email = "e8-backup-1440@example.com" }).IsValid());
        Assert.False((email with { Email = "e8-backup-390@example.test" }).IsValid());
        Assert.False((email with { Scenario = "backup-email-800" }).IsValid());
        Assert.False((email with { Scenario = "other" }).IsValid());
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WhatsAppBrowserRequest>(
            "{\"Kind\":\"recoveryNotice\",\"RecoveryId\":\"invalid\"}"));
    }

    private sealed class FixtureEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "Fixture";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
