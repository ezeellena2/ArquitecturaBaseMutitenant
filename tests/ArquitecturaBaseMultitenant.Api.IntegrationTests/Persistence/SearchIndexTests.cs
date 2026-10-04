using System.Reflection;
using System.Text.RegularExpressions;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

/// <summary>
/// Cada columna que busca un reader con <c>ApplySearch</c> tiene su índice GIN trigram sobre
/// <c>f_unaccent(lower(columna))</c>, la misma expresión de la búsqueda (paginado-y-busqueda.md). Las columnas salen
/// del código de los readers; las que se buscan dentro de una subconsulta van en <see cref="Indirect"/>.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class SearchIndexTests(ApiFactory factory)
{
    private const string ApplySearchCall = "ApplySearch(";

    // PlatformReader busca la organización sobre una consulta armada antes, y al Dueño con subconsultas.
    private static readonly SearchedColumn[] Indirect =
    [
        new("platform", "Tenants", "Name"),
        new("platform", "Tenants", "Slug"),
        new("identity", "AspNetUsers", "DisplayName"),
        new("identity", "AspNetUsers", "Email"),
        new("platform", "PendingTenantOwners", "Address"),
    ];

    private static readonly Regex Source = new(@"context\.(?<set>\w+)", RegexOptions.CultureInvariant);

    private static readonly Regex SimpleField = new(
        @"^(?<p>\w+)\s*=>\s*(?:\k<p>\.(?<property>\w+)|EF\.Property<string\??>\(\s*\k<p>\s*,\s*nameof\(\w+\.(?<property>\w+)\)\s*\))$",
        RegexOptions.CultureInvariant);

    [Fact]
    public async Task Every_column_a_reader_searches_has_a_trigram_index_with_the_search_expression()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = factory.Services.CreateAsyncScope();
        var model = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Model;
        var fields = ReaderSearchFields().ToArray();
        var resolved = fields.Where(field => field.Set is not null).Select(field => Column(model, field)).ToArray();

        // Sin la fuente en la misma sentencia, la columna tiene que estar en la lista de búsquedas indirectas.
        Assert.True(resolved.Length >= 10, "Los readers buscan al menos diez columnas con ApplySearch.");
        Assert.Empty(fields.Where(field => field.Set is null
                && !Indirect.Any(column => column.Column == field.Property))
            .Select(field => $"{field.File}: {field.Property}"));

        var indexes = await TrigramIndexesAsync(cancellationToken);
        Assert.Empty(resolved.Concat(Indirect).Distinct()
            .Where(column => !indexes.Any(index => index.Schema == column.Schema && index.Table == column.Table
                && index.Definition.Contains("f_unaccent(lower(", StringComparison.Ordinal)
                && index.Definition.Contains($"\"{column.Column}\"", StringComparison.Ordinal)))
            .Select(column => $"{column.Schema}.{column.Table}.{column.Column}"));
    }

    private static SearchedColumn Column(IModel model, SearchField field)
    {
        var set = typeof(ApplicationDbContext).GetProperty(field.Set!,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"{field.File}: context.{field.Set} is not a DbSet.");
        var entity = model.FindEntityType(set.PropertyType.GetGenericArguments()[0])!;
        var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
        var property = entity.FindProperty(field.Property)
            ?? throw new InvalidOperationException($"{field.File}: {field.Set}.{field.Property} is not mapped.");
        return new SearchedColumn(entity.GetSchema()!, entity.GetTableName()!, property.GetColumnName(table)!);
    }

    // Cada campo simple de ApplySearch (x => x.Columna o EF.Property) con el DbSet de su sentencia, si lo hay.
    private static IEnumerable<SearchField> ReaderSearchFields()
    {
        var readers = Path.Combine(FindRepositoryRoot(), "src", "ArquitecturaBaseMultitenant.Infrastructure",
            "Persistence", "Readers");
        foreach (var path in Directory.EnumerateFiles(readers, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(path);
            for (var call = source.IndexOf(ApplySearchCall, StringComparison.Ordinal); call >= 0;
                 call = source.IndexOf(ApplySearchCall, call + 1, StringComparison.Ordinal))
            {
                var statementStart = source.LastIndexOfAny([';', '{', '}'], call) + 1;
                var set = Source.Match(source[statementStart..call]);
                foreach (var argument in Arguments(source, call + ApplySearchCall.Length).Skip(1))
                {
                    var field = SimpleField.Match(argument);
                    if (field.Success)
                        yield return new SearchField(Path.GetFileName(path), set.Success ? set.Groups["set"].Value : null,
                            field.Groups["property"].Value);
                }
            }
        }
    }

    // Los argumentos de primer nivel de una llamada, desde después de su paréntesis de apertura.
    private static List<string> Arguments(string source, int start)
    {
        var arguments = new List<string>();
        var depth = 0;
        var from = start;
        for (var position = start; position < source.Length; position++)
        {
            switch (source[position])
            {
                case '(' or '[' or '{':
                    depth++;
                    break;
                case ')' or ']' or '}' when depth > 0:
                    depth--;
                    break;
                case ')':
                    arguments.Add(source[from..position].Trim());
                    return arguments;
                case ',' when depth == 0:
                    arguments.Add(source[from..position].Trim());
                    from = position + 1;
                    break;
            }
        }

        throw new InvalidOperationException("An ApplySearch call is not closed.");
    }

    private async Task<List<(string Schema, string Table, string Definition)>> TrigramIndexesAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT schemaname, tablename, indexdef FROM pg_indexes
            WHERE indexdef ILIKE '%USING gin%' AND indexdef LIKE '%gin_trgm_ops%'
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var indexes = new List<(string, string, string)>();
        while (await reader.ReadAsync(cancellationToken))
            indexes.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        return indexes;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ArquitecturaBaseMultitenant.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("ArquitecturaBaseMultitenant.slnx was not found.");
    }

    private sealed record SearchField(string File, string? Set, string Property);

    private sealed record SearchedColumn(string Schema, string Table, string Column);
}
