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
    public void Editing_platform_settings_changes_signup_rules_without_changing_the_deletion_grace()
    {
        var settings = PlatformSettings.Create(ConsumerSignupMode.Open, BusinessSignupMode.Open, 1);

        Assert.True(settings.Update(ConsumerSignupMode.Closed, BusinessSignupMode.RequiresApproval, 4).IsSuccess);
        Assert.Equal(SignupErrors.Closed, settings.CanRegisterConsumer().Error);
        Assert.Equal(BusinessSignupMode.RequiresApproval, settings.BusinessSignup);
        Assert.Equal(4, settings.MaxOwnedOrganizations);
        Assert.Equal(30, settings.AccountDeletionGraceDays);
    }

    [Theory]
    [InlineData(ConsumerSignupMode.Open, BusinessSignupMode.Open, 0)]
    [InlineData((ConsumerSignupMode)99, BusinessSignupMode.Open, 2)]
    [InlineData(ConsumerSignupMode.Open, (BusinessSignupMode)99, 2)]
    public void Invalid_platform_settings_do_not_change_saved_rules(ConsumerSignupMode consumer, BusinessSignupMode business, int limit)
    {
        var settings = PlatformSettings.Create(ConsumerSignupMode.Closed, BusinessSignupMode.Closed, 1);

        Assert.Equal(SettingsErrors.InvalidPlatformConfiguration, settings.Update(consumer, business, limit).Error);
        Assert.Equal(ConsumerSignupMode.Closed, settings.ConsumerSignup);
        Assert.Equal(BusinessSignupMode.Closed, settings.BusinessSignup);
        Assert.Equal(1, settings.MaxOwnedOrganizations);
    }

    [Fact]
    public void Closed_consumer_signup_blocks_personal_registration()
    {
        var closed = PlatformSettings.Create(ConsumerSignupMode.Closed, BusinessSignupMode.Open, 3);
        var open = PlatformSettings.Create(ConsumerSignupMode.Open, BusinessSignupMode.RequiresApproval, 3);

        Assert.Equal(SignupErrors.Closed, closed.CanRegisterConsumer().Error);
        Assert.True(open.CanRegisterConsumer().IsSuccess);
    }

    [Fact]
    public void Business_signup_allows_open_and_approval_modes_but_blocks_closed_mode()
    {
        var open = PlatformSettings.Create(ConsumerSignupMode.Closed, BusinessSignupMode.Open, 3);
        var approval = PlatformSettings.Create(ConsumerSignupMode.Closed, BusinessSignupMode.RequiresApproval, 3);
        var closed = PlatformSettings.Create(ConsumerSignupMode.Open, BusinessSignupMode.Closed, 3);

        Assert.True(open.CanRegisterBusiness().IsSuccess);
        Assert.True(approval.CanRegisterBusiness().IsSuccess);
        Assert.Equal(SignupErrors.BusinessClosed, closed.CanRegisterBusiness().Error);
    }

    [Fact]
    public void Business_signup_enforces_maximum_owned_organizations()
    {
        var settings = PlatformSettings.Create(ConsumerSignupMode.Open, BusinessSignupMode.Open, 2);

        Assert.True(settings.CanCreateBusinessOrganization(1).IsSuccess);
        var error = settings.CanCreateBusinessOrganization(2).Error;
        Assert.Equal(BusinessSignupErrors.LimitReachedCode, error.Code);
        // Tablero Registro-Empresa: «Llegaste al límite de 2 organizaciones propias.»
        Assert.Equal(2, error.Metadata?["limit"]);
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

    [Fact]
    public void Tenant_settings_can_be_updated_with_values_from_reference_catalogs()
    {
        var settings = TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS");

        var result = settings.Update(" en-US ", " America/New_York ", " USD ");

        Assert.True(result.IsSuccess);
        Assert.Equal("en-US", settings.DefaultCulture);
        Assert.Equal("America/New_York", settings.DefaultTimeZoneId);
        Assert.Equal("USD", settings.DefaultCurrency);
    }

    [Fact]
    public void Tenant_settings_reject_missing_defaults_without_changing_saved_values()
    {
        var settings = TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS");

        var result = settings.Update("en-US", " ", "USD");

        Assert.Equal(SettingsErrors.TenantConfigurationRequired, result.Error);
        Assert.Equal("es-AR", settings.DefaultCulture);
        Assert.Equal("America/Argentina/Buenos_Aires", settings.DefaultTimeZoneId);
        Assert.Equal("ARS", settings.DefaultCurrency);
    }
}
