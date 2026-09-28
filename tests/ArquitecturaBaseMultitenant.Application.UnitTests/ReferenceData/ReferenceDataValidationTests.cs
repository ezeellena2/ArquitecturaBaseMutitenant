using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Infrastructure.ReferenceData;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.ReferenceData;

public sealed class ReferenceDataValidationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record ReferenceRequest(
        string? Currency,
        string? Country,
        string? TimeZone,
        string? Culture,
        string? TaxIdType);

    private static readonly ReferenceRequest ValidRequest =
        new("ARS", "AR", "America/Argentina/Buenos_Aires", "es-AR", "AR-CUIT");

    [Fact]
    public async Task Enabled_values_and_optional_nulls_are_valid()
    {
        var catalog = new JsonReferenceDataCatalog();
        var validator = NewValidator(catalog);

        Assert.True((await validator.ValidateAsync(ValidRequest, Ct)).IsValid);
        Assert.True((await validator.ValidateAsync(new ReferenceRequest(null, null, null, null, null), Ct)).IsValid);
    }

    [Theory]
    [InlineData("Currency", "AED")]
    [InlineData("Country", "AD")]
    [InlineData("TimeZone", "Europe/Andorra")]
    [InlineData("Currency", "ZZZ")]
    [InlineData("Country", "ZZ")]
    [InlineData("TimeZone", "Unknown/Zone")]
    [InlineData("Culture", "zz-ZZ")]
    [InlineData("TaxIdType", "ZZ-ID")]
    public async Task Disabled_and_unknown_codes_are_rejected_for_new_data(string field, string code)
    {
        var catalog = new JsonReferenceDataCatalog();
        var validator = NewValidator(catalog);
        var request = field switch
        {
            "Currency" => ValidRequest with { Currency = code },
            "Country" => ValidRequest with { Country = code },
            "TimeZone" => ValidRequest with { TimeZone = code },
            "Culture" => ValidRequest with { Culture = code },
            "TaxIdType" => ValidRequest with { TaxIdType = code },
            _ => throw new InvalidOperationException(field),
        };

        var failure = Assert.Single((await validator.ValidateAsync(request, Ct)).Errors);

        Assert.Equal(field, failure.PropertyName);
    }

    [Fact]
    public async Task Disabled_culture_and_tax_type_remain_readable_but_cannot_be_selected()
    {
        var catalog = new JsonReferenceDataCatalog();
        var culture = (await ((ICultureCatalog)catalog).FindAsync("en-US", Ct))! with { IsEnabled = false };
        var taxType = (await ((ITaxIdTypeCatalog)catalog).FindAsync("AR-DNI", Ct))! with { IsEnabled = false };
        var cultures = new CultureCatalogStub(culture);
        var taxTypes = new TaxIdTypeCatalogStub(taxType);
        var validator = NewValidator(catalog, cultures, taxTypes);

        Assert.False((await cultures.FindAsync(culture.Code, Ct))!.IsEnabled);
        Assert.False((await taxTypes.FindAsync(taxType.Code, Ct))!.IsEnabled);
        var result = await validator.ValidateAsync(ValidRequest with { Culture = culture.Code, TaxIdType = taxType.Code }, Ct);

        Assert.Equal(["Culture", "TaxIdType"], result.Errors.Select(error => error.PropertyName));
    }

    private static ReferenceRequestValidator NewValidator(
        JsonReferenceDataCatalog catalog,
        ICultureCatalog? cultures = null,
        ITaxIdTypeCatalog? taxTypes = null) =>
        new((ICurrencyCatalog)catalog, (ICountryCatalog)catalog, (ITimeZoneCatalog)catalog,
            cultures ?? (ICultureCatalog)catalog, taxTypes ?? (ITaxIdTypeCatalog)catalog);

    private sealed class ReferenceRequestValidator : AbstractValidator<ReferenceRequest>
    {
        public ReferenceRequestValidator(
            ICurrencyCatalog currencies,
            ICountryCatalog countries,
            ITimeZoneCatalog timeZones,
            ICultureCatalog cultures,
            ITaxIdTypeCatalog taxIdTypes)
        {
            RuleFor(request => request.Currency).ValidCurrency(currencies);
            RuleFor(request => request.Country).ValidCountry(countries);
            RuleFor(request => request.TimeZone).ValidTimeZone(timeZones);
            RuleFor(request => request.Culture).ValidCulture(cultures);
            RuleFor(request => request.TaxIdType).ValidTaxIdType(taxIdTypes);
        }
    }

    private sealed class CultureCatalogStub(CultureCatalogEntry row) : ICultureCatalog
    {
        public Task<IReadOnlyList<CultureCatalogEntry>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CultureCatalogEntry>>([row]);

        public Task<CultureCatalogEntry?> FindAsync(string code, CancellationToken cancellationToken) =>
            Task.FromResult(code == row.Code ? row : null);
    }

    private sealed class TaxIdTypeCatalogStub(TaxIdTypeCatalogEntry row) : ITaxIdTypeCatalog
    {
        public Task<IReadOnlyList<TaxIdTypeCatalogEntry>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TaxIdTypeCatalogEntry>>([row]);

        public Task<TaxIdTypeCatalogEntry?> FindAsync(string code, CancellationToken cancellationToken) =>
            Task.FromResult(code == row.Code ? row : null);
    }
}
