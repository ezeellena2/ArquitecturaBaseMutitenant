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

    private sealed class FixtureEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "Fixture";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
