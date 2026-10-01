using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Settings;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Settings;

/// <summary>
/// Comprueba las reglas de registro y los valores de configuración global y por espacio. Exige que los
/// formatos predeterminados provengan de datos de referencia.
/// </summary>
public sealed class PlatformSettingsTests
{
    [Fact]
    public void Closed_consumer_signup_blocks_personal_registration()
    {
        var closed = PlatformSettings.Create(ConsumerSignupMode.Closed, BusinessSignupMode.Open, 3);
        var open = PlatformSettings.Create(ConsumerSignupMode.Open, BusinessSignupMode.RequiresApproval, 3);

        Assert.Equal(SignupErrors.Closed, closed.CanRegisterConsumer().Error);
        Assert.True(open.CanRegisterConsumer().IsSuccess);
    }

    [Fact]
    public void Platform_settings_have_a_single_identity_and_thirty_day_deletion_grace()
    {
        var settings = PlatformSettings.Create(ConsumerSignupMode.Open, BusinessSignupMode.RequiresApproval, 4);

        Assert.Equal(PlatformSettings.SingletonId, settings.Id);
        Assert.Equal(ConsumerSignupMode.Open, settings.ConsumerSignup);
        Assert.Equal(BusinessSignupMode.RequiresApproval, settings.BusinessSignup);
        Assert.Equal(4, settings.MaxOwnedOrganizations);
        Assert.Equal(30, settings.AccountDeletionGraceDays);
    }

    [Fact]
    public void Personal_defaults_are_supplied_by_reference_data_not_a_domain_list()
    {
        var settings = TenantSettings.Create("en-US", "America/New_York", "USD");

        Assert.Equal(Guid.Empty, settings.TenantId);
        Assert.Equal("en-US", settings.DefaultCulture);
        Assert.Equal("America/New_York", settings.DefaultTimeZoneId);
        Assert.Equal("USD", settings.DefaultCurrency);
    }

    [Fact]
    public void Tenant_settings_column_starts_unset_for_the_interceptor_to_stamp()
    {
        var settings = TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS");

        Assert.Equal(Guid.Empty, settings.TenantId);
    }
}
