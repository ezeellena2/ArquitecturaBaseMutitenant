using System.Globalization;
using System.Reflection;
using System.Resources;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Assistant;
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

    /// <summary>
    /// El texto de un código de error llega tal cual al <c>detail</c> del ProblemDetails, que no completa marcadores: un número
    /// va en la metadata del error y la pantalla arma su frase, como el cupo agotado del asistente (283). El texto de un campo
    /// con números va en otra clave, como <c>WhatsApp.Settings.ChatRetentionRange</c>.
    /// </summary>
    [Fact]
    public void Error_code_texts_have_no_placeholders()
    {
        var assembly = typeof(ErrorTexts).Assembly;
        var codes = ErrorCodes(typeof(Error).Assembly);
        var sources = assembly.GetManifestResourceNames()
            .Where(name => name.Contains(".Modules.", StringComparison.Ordinal) && name.EndsWith("Texts.resources", StringComparison.Ordinal))
            .Select(name => new ResourceManager(name[..^".resources".Length], assembly))
            .Append(ErrorTexts.ResourceManager).ToArray();
        CultureInfo[] cultures = [CultureInfo.InvariantCulture, CultureInfo.GetCultureInfo("en")];

        var templated = (from source in sources
                         from culture in cultures
                         let set = source.GetResourceSet(culture, createIfNotExists: true, tryParents: false)
                         from code in codes
                         let text = set?.GetString(code)
                         where text is not null && ResourceParityTests.Placeholders(text).Length > 0
                         select $"{culture.Name}:{code}").ToArray();

        Assert.Contains(AssistantErrors.LimitDailyReachedCode, codes);
        Assert.Empty(templated);
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

    /// <summary>Los códigos de las clases <c>*Errors</c> del dominio: sus constantes <c>*Code</c> y sus errores sin argumentos.</summary>
    private static HashSet<string> ErrorCodes(Assembly assembly)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in assembly.GetTypes().Where(type => type.Name.EndsWith("Errors", StringComparison.Ordinal) && !type.ContainsGenericParameters))
        {
            foreach (var field in type.GetFields(flags))
            {
                if (field.IsLiteral && field.FieldType == typeof(string) && field.Name.EndsWith("Code", StringComparison.Ordinal))
                    codes.Add((string)field.GetRawConstantValue()!);
                else if (!field.IsLiteral && typeof(Error).IsAssignableFrom(field.FieldType) && field.GetValue(null) is Error error)
                    codes.Add(error.Code);
            }
            foreach (var property in type.GetProperties(flags).Where(property => typeof(Error).IsAssignableFrom(property.PropertyType) && property.GetIndexParameters().Length == 0))
                if (property.GetValue(null) is Error error) codes.Add(error.Code);
        }
        return codes;
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
