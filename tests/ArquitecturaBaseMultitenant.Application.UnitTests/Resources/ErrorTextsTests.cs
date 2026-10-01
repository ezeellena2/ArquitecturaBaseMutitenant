using System.Globalization;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.ReferenceData;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Resources;

/// <summary>
/// Comprueba la traducción de errores según la cultura de interfaz y su fallback. Distingue un código
/// desconocido de uno traducido.
/// </summary>
public sealed class ErrorTextsTests
{
    [Theory]
    [InlineData("es-AR", "Revisá los campos marcados.")]
    [InlineData("en-US", "Check the highlighted fields.")]
    [InlineData("fr-FR", "Revisá los campos marcados.")]
    public void Error_code_follows_the_current_ui_culture_or_neutral_fallback(string culture, string expected)
    {
        using var scope = new CultureScope(culture);

        Assert.Equal(expected, ErrorTexts.Find(ValidationError.ErrorCode));
    }

    [Fact]
    public void Unknown_code_is_not_found()
    {
        Assert.Null(ErrorTexts.Find("Test.Unknown.Code"));
        Assert.Equal("Test.Unknown.Code", ErrorTexts.Get("Test.Unknown.Code"));
    }

    [Theory]
    [InlineData("es-AR", ErrorType.NotFound, "No encontrado")]
    [InlineData("en-US", ErrorType.NotFound, "Not found")]
    [InlineData("es-AR", ErrorType.Validation, "Datos inválidos")]
    [InlineData("en-US", ErrorType.Failure, "Server error")]
    public void Title_depends_on_the_error_type(string culture, ErrorType type, string expected)
    {
        using var scope = new CultureScope(culture);

        Assert.Equal(expected, ErrorTexts.Title(type));
    }

    [Fact]
    public void Reference_data_value_object_errors_are_localized_in_both_languages()
    {
        using (var scope = new CultureScope("es-AR"))
        {
            Assert.Equal("Ingresá un código de moneda válido.", ErrorTexts.Get(CurrencyCodeErrors.InvalidCode));
            Assert.Equal("Ingresá una cultura válida.", ErrorTexts.Get(CultureCodeErrors.InvalidCode));
        }

        using (var scope = new CultureScope("en-US"))
        {
            Assert.Equal("Enter a valid currency code.", ErrorTexts.Get(CurrencyCodeErrors.InvalidCode));
            Assert.Equal("Enter a valid culture.", ErrorTexts.Get(CultureCodeErrors.InvalidCode));
        }
    }

    [Theory]
    [InlineData("es-AR", "Este campo es obligatorio.")]
    [InlineData("en-US", "This field is required.")]
    public void Validation_texts_follow_the_current_ui_culture(string culture, string expected)
    {
        using var scope = new CultureScope(culture);

        Assert.Equal(expected, ValidationTexts.Required);
    }

    [Fact]
    public async Task Supported_cultures_are_loaded_from_the_catalog_and_filter_disabled_rows()
    {
        var catalog = (ICultureCatalog)new JsonReferenceDataCatalog();
        var source = await catalog.ListAsync(TestContext.Current.CancellationToken);
        var defaultCulture = Assert.Single(source, entry => entry.IsDefault);
        var disabled = source.First(entry => !entry.IsDefault) with { IsEnabled = false };
        var added = defaultCulture with
        {
            Code = "pt-BR",
            LanguageCode = "pt",
            CountryCode = "BR",
            IsDefault = false,
        };
        var rows = source.Where(entry => entry.Code != disabled.Code).Append(disabled).Append(added).ToArray();

        var supported = await SupportedCultures.LoadAsync(new CultureCatalogStub(rows), TestContext.Current.CancellationToken);

        Assert.Equal(defaultCulture.Code, supported.DefaultCulture);
        Assert.Contains(added.Code, supported.Codes);
        Assert.DoesNotContain(disabled.Code, supported.Codes);
    }

    [Fact]
    public async Task Supported_cultures_require_exactly_one_enabled_default()
    {
        var catalog = (ICultureCatalog)new JsonReferenceDataCatalog();
        var rows = await catalog.ListAsync(TestContext.Current.CancellationToken);
        var noDefault = rows.Select(entry => entry with { IsDefault = false }).ToArray();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SupportedCultures.LoadAsync(new CultureCatalogStub(noDefault), TestContext.Current.CancellationToken));
    }

    private sealed class CultureCatalogStub(IReadOnlyList<CultureCatalogEntry> rows) : ICultureCatalog
    {
        public Task<IReadOnlyList<CultureCatalogEntry>> ListAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(rows);
        }

        public Task<CultureCatalogEntry?> FindAsync(string code, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(rows.FirstOrDefault(entry => entry.Code == code));
        }
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;

        public CultureScope(string name)
        {
            var culture = CultureInfo.GetCultureInfo(name);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _culture;
            CultureInfo.CurrentUICulture = _uiCulture;
        }
    }
}
