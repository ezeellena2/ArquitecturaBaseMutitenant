using ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;
using Microsoft.Extensions.Options;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Application.Configuration.Auth;
using Microsoft.Extensions.DependencyInjection;
namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;
public sealed class WhatsAppChannelSettingsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(null, false, false)]
    [InlineData(WhatsAppNumberStatus.Connected, true, true)]
    [InlineData(WhatsAppNumberStatus.PaymentMissing, false, true)]
    public async Task Login_channel_follows_the_connected_platform_number_and_its_payment(WhatsAppNumberStatus? status, bool codes, bool attending)
    {
        var reader = new Reader();
        var (settings, state) = Settings(reader, new WhatsAppOptions());
        reader.Row = status is { } value ? new WhatsAppPlatformChannelRow(Guid.NewGuid(), "fixture-number", "+15551234567", value, true) : null;
        await state.RefreshAsync(Ct);
        var channel = new WhatsAppLoginCodeChannel(settings, null!, null!);
        Assert.Equal("whatsapp", channel.Key);
        Assert.Equal(LoginMethodType.Phone, channel.SupportedMethodType);
        Assert.Equal(codes, channel.IsAvailable);
        Assert.Equal(attending, settings.IsAttending);
        Assert.Equal(attending ? "fixture-number" : null, settings.PhoneNumberId);

        reader.Row = null;
        await state.RefreshAsync(Ct);
        Assert.False(channel.IsAvailable);
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
    public void Configured_contact_phone_is_canonical_and_never_technical_id(string? display, string? expected) =>
        Assert.Equal(expected, WhatsAppChannelBootstrapService.Display(display)?.Value);
    [Theory]
    [InlineData("AR", "+14155552671", false)]
    [InlineData("US", "+5491134567890", true)]
    public async Task Policy_uses_actual_region_of_E164_and_ignores_declared_country(string declaredCountry, string phone, bool allowed)
    {
        var reader = new Reader { Row = new WhatsAppPlatformChannelRow(Guid.NewGuid(), "technical-id", null, WhatsAppNumberStatus.Connected, false) };
        var (settings, state) = Settings(reader, new WhatsAppOptions { AllowedCountries = ["AR"] });
        await state.RefreshAsync(Ct);
        var channel = new WhatsAppLoginCodeChannel(settings, null!, null!);
        var result = channel.ValidateNewDestination(LoginCodeDestination.ForPhone(PhoneNumber.Create(phone).Value, "whatsapp"), declaredCountry);
        Assert.Equal(allowed, result.IsSuccess);
        if (!allowed) Assert.Equal("WhatsApp.Phone.CountryNotAllowed", result.Error.Code);
    }

    private static (WhatsAppChannelSettings Settings, WhatsAppPlatformChannelState State) Settings(Reader reader, WhatsAppOptions options)
    {
        var provider = new ServiceCollection().AddSingleton<IWhatsAppChannelReader>(reader).BuildServiceProvider();
        var state = new WhatsAppPlatformChannelState(provider.GetRequiredService<IServiceScopeFactory>(), Options.Create(options));
        return (new WhatsAppChannelSettings(Options.Create(options),
            Options.Create(new ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email.EmailOptions()), state), state);
    }

    /// <summary>Solo el número vigente de Plataforma; las demás lecturas no se usan en estas pruebas.</summary>
    private sealed class Reader : IWhatsAppChannelReader
    {
        public WhatsAppPlatformChannelRow? Row { get; set; }
        public Task<WhatsAppPlatformChannelRow?> FindPlatformStateAsync(CancellationToken ct) => Task.FromResult(Row);
        public Task<PagedResult<WhatsAppNumberResponse>> ListOrganizationAsync(WhatsAppNumberListRequest request, DateTime nowUtc, CancellationToken ct) => throw new NotSupportedException();
        public Task<WhatsAppNumberResponse?> FindOrganizationAsync(Guid id, DateTime nowUtc, CancellationToken ct) => throw new NotSupportedException();
        public Task<int> CountOrganizationAsync(WhatsAppNumberStatus? status, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<WhatsAppNumberPendingRow>> ListPaymentMissingAsync(int limit, CancellationToken ct) => throw new NotSupportedException();
        public Task<WhatsAppPlatformNumberRow?> FindPlatformAsync(DateTime nowUtc, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> IsActiveAsync(string phoneNumberId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> ListOrganizationIdsAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<WhatsAppInboundChannel?> FindInboundAsync(Guid? channelId, string phoneNumberId, CancellationToken ct) => throw new NotSupportedException();
        public Task<WhatsAppSendingChannelRow?> FindSendingAsync(Guid? channelId, string phoneNumberId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence.WhatsAppTemplateAccountRow>> ListTemplateAccountsAsync(bool platform, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence.WhatsAppTemplateAccountRow>> ListAllTemplateAccountsAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> HasActiveAccountAsync(string wabaId, CancellationToken ct) => throw new NotSupportedException();
    }
}
