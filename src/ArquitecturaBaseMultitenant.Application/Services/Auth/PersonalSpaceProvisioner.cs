using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Prepara el espacio antes de fijar tenant y lo agrega dentro de la UoW del llamador.</summary>
public interface IPersonalSpaceProvisioner
{
    Task<PersonalSpaceDraft> PrepareAsync(string? cultureCode, string? browserTimeZoneId,
        CancellationToken cancellationToken);

    void Stage(PersonalSpaceDraft draft, Guid userId);
}

public sealed record PersonalSpaceDraft(
    Tenant Tenant,
    TenantSettings Settings,
    string Culture,
    string TimeZoneId);

internal sealed class PersonalSpaceProvisioner(
    ICultureCatalog cultures,
    ICountryCatalog countries,
    ICurrencyCatalog currencies,
    ITimeZoneCatalog timeZones,
    TenantSpaceProvisioner provisioner) : IPersonalSpaceProvisioner
{
    public async Task<PersonalSpaceDraft> PrepareAsync(string? cultureCode, string? browserTimeZoneId,
        CancellationToken cancellationToken)
    {
        var enabledCultures = (await cultures.ListAsync(cancellationToken))
            .Where(row => row.IsEnabled).ToArray();
        var defaultCulture = enabledCultures.Single(row => row.IsDefault);
        var selected = enabledCultures.FirstOrDefault(row =>
            string.Equals(row.Code, cultureCode, StringComparison.OrdinalIgnoreCase)) ?? defaultCulture;
        var country = await countries.FindAsync(selected.CountryCode, cancellationToken);
        var defaultCountry = await countries.FindAsync(defaultCulture.CountryCode, cancellationToken);
        if (country is not { IsEnabled: true })
        {
            country = defaultCountry;
        }
        if (country is not { IsEnabled: true })
        {
            throw new InvalidOperationException("The enabled default culture has no enabled country.");
        }

        var currencyCode = country.DefaultCurrencyCode ?? defaultCountry?.DefaultCurrencyCode;
        var currency = currencyCode is null ? null : await currencies.FindAsync(currencyCode, cancellationToken);
        if (currency is not { IsEnabled: true })
        {
            throw new InvalidOperationException("The selected culture has no enabled default currency.");
        }

        var countryZone = country.DefaultTimeZoneId ?? defaultCountry?.DefaultTimeZoneId;
        var requestedZone = browserTimeZoneId is null ? null :
            await timeZones.FindAsync(browserTimeZoneId, cancellationToken);
        var timeZoneId = requestedZone is { IsEnabled: true }
            ? requestedZone.Id : countryZone;
        var timeZone = timeZoneId is null ? null : await timeZones.FindAsync(timeZoneId, cancellationToken);
        if (timeZone is not { IsEnabled: true })
        {
            throw new InvalidOperationException("The selected culture has no enabled default time zone.");
        }

        return new PersonalSpaceDraft(Tenant.CreatePersonal("Personal"),
            TenantSettings.Create(selected.Code, timeZone.Id, currency.Code), selected.Code, timeZone.Id);
    }

    public void Stage(PersonalSpaceDraft draft, Guid userId)
    {
        ArgumentNullException.ThrowIfNull(draft);
        provisioner.Stage(draft.Tenant, draft.Settings, [userId]);
    }
}
