using ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;
using Microsoft.Extensions.Options;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Application.Configuration.Auth;
namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;
public sealed class WhatsAppChannelSettingsTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData(" \t", false)]
    [InlineData("fixture-number", true)]
    public void Login_channel_availability_tracks_configured_number_and_subsequent_changes(string? number, bool available)
    {
        var options = new ChangingWhatsAppOptions { Value = new WhatsAppOptions { PhoneNumberId = number } };
        var settings = new WhatsAppChannelSettings(options,
            Options.Create(new ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email.EmailOptions()));
        var channel = new WhatsAppLoginCodeChannel(settings, null!, null!);
        Assert.Equal("whatsapp", channel.Key);
        Assert.Equal(LoginMethodType.Phone, channel.SupportedMethodType);
        Assert.Equal(available, channel.IsAvailable);

        options.Value = new WhatsAppOptions { PhoneNumberId = available ? " \t" : "fixture-number" };
        Assert.Equal(!available, channel.IsAvailable);
    }

    private sealed class ChangingWhatsAppOptions : IOptions<WhatsAppOptions>
    {
        public WhatsAppOptions Value { get; set; } = new();
    }

    [Theory]
    [InlineData(10, true)]
    [InlineData(5, false)]
    [InlineData(15, false)]
    public void Enabled_authentication_checks_approved_template_lifetime(int minutes, bool success)
    {
        var validator = new WhatsAppOptionsValidator(Options.Create(new LoginCodeOptions { LifetimeMinutes = minutes }));
        Assert.Equal(success, validator.Validate(null, new WhatsAppOptions { PhoneNumberId = "id", AccessToken = "fixture" }).Succeeded);
        Assert.True(validator.Validate(null, new WhatsAppOptions()).Succeeded);
    }
    [Theory]
    [InlineData("15551234567", "+15551234567")]
    [InlineData("+1 555 123 4567", "+15551234567")]
    [InlineData(null, null)]
    [InlineData("invalid", null)]
    public void Contact_phone_is_canonical_and_never_technical_id(string? display, string? expected)
    {
        var settings = new WhatsAppChannelSettings(Options.Create(new WhatsAppOptions { PhoneNumberId = "technical-id", DisplayPhoneNumber = display }), Options.Create(new ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email.EmailOptions()));
        Assert.Equal(expected, settings.ContactPhone);
    }
    [Theory]
    [InlineData("AR", "+14155552671", false)]
    [InlineData("US", "+5491134567890", true)]
    public void Policy_uses_actual_region_of_E164_and_ignores_declared_country(string declaredCountry, string phone, bool allowed)
    {
        var settings = new WhatsAppChannelSettings(Options.Create(new WhatsAppOptions { PhoneNumberId = "technical-id", AllowedCountries = ["AR"] }), Options.Create(new ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email.EmailOptions()));
        var channel = new WhatsAppLoginCodeChannel(settings, null!, null!);
        var result = channel.ValidateNewDestination(LoginCodeDestination.ForPhone(PhoneNumber.Create(phone).Value, "whatsapp"), declaredCountry);
        Assert.Equal(allowed, result.IsSuccess);
        if (!allowed) Assert.Equal("WhatsApp.Phone.CountryNotAllowed", result.Error.Code);
    }
}
