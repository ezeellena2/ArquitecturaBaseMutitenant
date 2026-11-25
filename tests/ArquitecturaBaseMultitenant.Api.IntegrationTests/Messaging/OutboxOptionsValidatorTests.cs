using ArquitecturaBaseMultitenant.Application.Configuration.Messaging;
using ArquitecturaBaseMultitenant.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Messaging;

/// <summary>
/// N8 y ADR 0066 §5: cada canal tiene su espera y su tope de intentos, y la entrega más lenta con su confirmación cabe en el
/// apagado del host; si no, la Api no arranca.
/// </summary>
public sealed class OutboxOptionsValidatorTests
{
    [Fact]
    public void The_defaults_fit_in_the_default_shutdown()
    {
        Assert.True(Validate(new OutboxOptions(), new HostOptions()).Succeeded);
    }

    [Fact]
    public void A_channel_takes_its_own_wait_and_attempts_and_the_rest_from_the_general_ones()
    {
        var options = Bind(new Dictionary<string, string?>
        {
            ["Messaging:Outbox:Channels:whatsapp:MaxAttempts"] = "3",
            ["Messaging:Outbox:Channels:email:DeliveryTimeoutSeconds"] = "12",
        });

        Assert.Equal(3, options.MaxAttemptsFor("whatsapp"));
        Assert.Equal(TimeSpan.FromSeconds(15), options.DeliveryTimeoutFor("whatsapp"));
        Assert.Equal(TimeSpan.FromSeconds(12), options.DeliveryTimeoutFor("email"));
        Assert.Equal(5, options.MaxAttemptsFor("email"));
        Assert.Equal(5, options.MaxAttemptsFor("other"));
        Assert.True(Validate(options, new HostOptions()).Succeeded);
    }

    [Fact]
    public void A_delivery_that_does_not_fit_in_the_shutdown_is_rejected()
    {
        var slowChannel = Bind(new Dictionary<string, string?> { ["Messaging:Outbox:Channels:whatsapp:DeliveryTimeoutSeconds"] = "25" });
        var failure = Validate(slowChannel, new HostOptions { ShutdownTimeout = TimeSpan.FromSeconds(30) });
        Assert.True(failure.Failed);
        Assert.Contains("shutdown", failure.FailureMessage, StringComparison.Ordinal);
        Assert.True(Validate(slowChannel, new HostOptions { ShutdownTimeout = TimeSpan.FromSeconds(60) }).Succeeded);
        Assert.True(Validate(new OutboxOptions(), new HostOptions { ShutdownTimeout = TimeSpan.FromSeconds(20) }).Failed);
    }

    [Fact]
    public void A_channel_value_out_of_range_is_rejected()
    {
        var options = Bind(new Dictionary<string, string?>
        {
            ["Messaging:Outbox:Channels:email:MaxAttempts"] = "0",
            ["Messaging:Outbox:Channels:email:DeliveryTimeoutSeconds"] = "61",
        });
        var result = Validate(options, new HostOptions { ShutdownTimeout = TimeSpan.FromMinutes(5) });
        Assert.True(result.Failed);
        Assert.Contains("Channels:email:MaxAttempts", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("Channels:email:DeliveryTimeoutSeconds", result.FailureMessage, StringComparison.Ordinal);
    }

    private static OutboxOptions Bind(Dictionary<string, string?> values)
    {
        var options = new OutboxOptions();
        new ConfigurationBuilder().AddInMemoryCollection(values).Build().GetSection(OutboxOptions.SectionName).Bind(options);
        return options;
    }

    private static ValidateOptionsResult Validate(OutboxOptions options, HostOptions host) =>
        new OutboxOptionsValidator(Options.Create(host)).Validate(null, options);
}
