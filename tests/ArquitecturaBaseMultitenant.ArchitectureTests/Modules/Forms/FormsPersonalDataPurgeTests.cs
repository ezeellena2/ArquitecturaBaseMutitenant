using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ArquitecturaBaseMultitenant.ArchitectureTests.Modules.Forms;

/// <summary>
/// ADR 0062 §4 para Formularios: cada tabla del módulo con datos personales nombra quién la purga o por qué se conserva, con el
/// criterio de <see cref="PersonalDataPurgeTests"/>, que arma solo el modelo del núcleo. Este arma el del núcleo con el aporte de
/// <c>FormsModelContributor</c> y mira solo las tablas que suma. Empieza con las pistas del núcleo; una etapa de Forms suma las
/// suyas cuando una columna guarda datos de una persona sin decirlo en su nombre.
/// </summary>
public sealed class FormsPersonalDataPurgeTests
{
    private const string FormsContributor = "FormsModelContributor";

    private static readonly string[] PersonalColumns =
    [
        "Email", "Phone", "Destination", "Identifier", "IpAddress", "Address", "UserAgent", "UserName", "DisplayName",
        "FullName", "Contact", "Recipient", "ProviderKey", "Subject", "Protected", "UserId",
    ];

    /// <summary>Quién purga cada tabla, o por qué se conserva.</summary>
    private static readonly Dictionary<string, Retained> Retention = new(StringComparer.Ordinal)
    {
        ["tenant.FormWorkLeases"] = Kept("la barrera de archivos de una persona (C11): se suelta al terminar y, si el proceso se cae, vence sola a los dos minutos"),
    };

    [Fact]
    public void Every_forms_table_with_personal_data_names_who_purges_it_or_why_it_is_kept()
    {
        var personal = PersonalTables();

        // El detector ve las tablas del módulo y sus columnas: si dejara de verlas, el control pasaría en silencio.
        Assert.Contains("UserId", personal["tenant.FormWorkLeases"]);

        string[] unnamed = [.. personal.Keys.Except(Retention.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .Select(table => $"{table}: {string.Join(", ", personal[table])}")];
        Assert.True(unnamed.Length == 0, string.Join(Environment.NewLine, unnamed));
        Assert.Empty(Retention.Keys.Except(personal.Keys, StringComparer.Ordinal));
        Assert.All(Retention.Values, retained => Assert.False(string.IsNullOrWhiteSpace(retained.Why)));

        // Quien purga existe: un trabajo de fondo de Infrastructure, por su nombre, o un trigger de la base.
        var workers = typeof(ApplicationDbContext).Assembly.GetTypes().Select(type => type.Name).ToHashSet(StringComparer.Ordinal);
        string[] missing = [.. Retention.Values.Select(retained => retained.PurgedBy).OfType<string>()
            .Where(purger => !purger.StartsWith("trigger ", StringComparison.Ordinal) && !workers.Contains(purger))];
        Assert.True(missing.Length == 0, string.Join(", ", missing));
    }

    /// <summary>Las tablas que el aporte de Formularios suma al modelo del núcleo.</summary>
    private static Dictionary<string, string[]> PersonalTables()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=architecture_guard");
        options.UseOpenIddict<Guid>();
        var forms = Assert.Single(PersistenceModelContributors.Discover(), contributor => contributor.GetType().Name == FormsContributor);
        using var core = new ApplicationDbContext(options.Options, new NoTenantContext(), []);
        using var withForms = new ApplicationDbContext(options.Options, new NoTenantContext(), [forms]);
        var coreTables = core.Model.GetEntityTypes().Select(TableOf).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var formsTables = withForms.Model.GetEntityTypes().Where(entity => TableOf(entity) is { } table && !coreTables.Contains(table)).ToArray();
        Assert.NotEmpty(formsTables);
        return formsTables
            .GroupBy(entity => TableOf(entity)!, StringComparer.Ordinal)
            .Select(table => (Table: table.Key, Columns: table
                .SelectMany(ColumnsOf)
                .Where(name => PersonalColumns.Any(hint => name.Contains(hint, StringComparison.Ordinal)))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray()))
            .Where(table => table.Columns.Length > 0)
            .ToDictionary(table => table.Table, table => table.Columns, StringComparer.Ordinal);
    }

    private static string? TableOf(IEntityType entity) =>
        entity.GetTableName() is { } table ? $"{entity.GetSchema()}.{table}" : null;

    // Las columnas de las propiedades comunes y de las complejas, también las anidadas, con su nombre en la tabla.
    private static IEnumerable<string> ColumnsOf(ITypeBase type) =>
        type.GetProperties().Select(property => property.GetColumnName())
            .Concat(type.GetComplexProperties().SelectMany(complex => ColumnsOf(complex.ComplexType)));

    private static Retained Kept(string why) => new(null, why);

    /// <param name="PurgedBy">Quién la purga, o null si se conserva.</param>
    /// <param name="Why">Qué borra y cuándo, o por qué se conserva.</param>
    private sealed record Retained(string? PurgedBy, string Why);

    private sealed class NoTenantContext : ITenantContext
    {
        public Guid? TenantId => null;

        public TenantKind? TenantKind => null;

        public Guid RequiredTenantId => throw new NotSupportedException();
    }
}
