using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

/// <summary>
/// Comprueba índices por espacio, campo de orden e identificador para cada orden admitido. Protege el
/// soporte del paginado estable en base: el Widget de prueba y cada <see cref="SortMap{T}"/> de los lectores reales
/// (paginado-y-busqueda.md). En una tabla privada el índice es <c>(TenantId, campo, Id)</c>; en una global termina en
/// <c>(campo, Id)</c>, detrás de la columna por la que filtra el lector si la hay (TenantDomains, por organización).
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class SortIndexTests(ApiFactory factory)
{
    // Los SortMap de una proyección no ordenan una tabla: cada campo dice la columna que ordena, o null cuando sale de la
    // cuenta global (nombre o correo) de una lista acotada a una organización o empresa, o de una cuenta en SQL (el estado
    // de una persona, por su circuito), que ningún índice de una tabla resuelve. Un SortMap de proyección nuevo entra acá.
    private static readonly ProjectedSortField[] Projected =
    [
        new("CompanyMembershipReader.Sort", "displayName", null),
        new("CompanyMembershipReader.Sort", "email", null),
        new("CompanyMembershipReader.Sort", "state", null),
        new("CompanyMembershipReader.CandidateSort", "displayName", null),
        new("OrganizationUserReader.Sort", "displayName", null),
        new("OrganizationUserReader.Sort", "email", null),
        new("OrganizationUserReader.Sort", "state", null),
        new("OrganizationUserReader.Sort", "createdAtUtc", new("tenant", "Members", "CreatedAtUtc")),
        new("PlatformAccountReader.Sort", "name", new("identity", "AspNetUsers", "DisplayName")),
        // WhatsApp: un equipo cuenta sus integrantes; una plantilla resume su categoría, su estado y su fecha con lo que dijo Meta;
        // una difusión cuenta entregados y leídos de sus destinatarios, y su fecha elige entre tres. Los destinatarios son los de
        // una difusión, con su estado y su fecha calculados y el nombre del contacto.
        new("WhatsAppTeamReader.Sort", "name", new("tenant", "WhatsAppTeams", "Name")),
        new("WhatsAppTeamReader.Sort", "inbox", new("tenant", "WhatsAppTeams", "Inbox")),
        new("WhatsAppTeamReader.Sort", "memberCount", null),
        new("WhatsAppTeamReader.Sort", "notice", new("tenant", "WhatsAppTeams", "NotifyAfterMinutes")),
        new("WhatsAppTemplateReader.Sort", "name", new("tenant", "WhatsAppTemplates", "Name")),
        new("WhatsAppTemplateReader.Sort", "category", null),
        new("WhatsAppTemplateReader.Sort", "status", null),
        new("WhatsAppTemplateReader.Sort", "updatedAtUtc", null),
        new("WhatsAppBroadcastReader.Sort", "name", new("tenant", "WhatsAppBroadcasts", "Name")),
        new("WhatsAppBroadcastReader.Sort", "template", new("tenant", "WhatsAppBroadcasts", "TemplateName")),
        new("WhatsAppBroadcastReader.Sort", "recipients", new("tenant", "WhatsAppBroadcasts", "Recipients")),
        new("WhatsAppBroadcastReader.Sort", "status", new("tenant", "WhatsAppBroadcasts", "Status")),
        new("WhatsAppBroadcastReader.Sort", "delivered", null),
        new("WhatsAppBroadcastReader.Sort", "read", null),
        new("WhatsAppBroadcastReader.Sort", "date", null),
        new("WhatsAppBroadcastReader.RecipientSort", "contact", null),
        new("WhatsAppBroadcastReader.RecipientSort", "number", null),
        new("WhatsAppBroadcastReader.RecipientSort", "status", null),
        new("WhatsAppBroadcastReader.RecipientSort", "changedAtUtc", null),
    ];

    private static readonly Regex BtreeColumns = new(@"USING btree \((?<columns>[^)]*)\)", RegexOptions.CultureInvariant);

    [Fact]
    public async Task Every_widget_sort_field_has_tenant_first_index_with_id_tie_breaker()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = await WidgetQueryFixture.CreateAsync(factory, Guid.NewGuid(), cancellationToken);
        var entity = fixture.Context.Model.FindEntityType(typeof(Widget));
        Assert.NotNull(entity);

        foreach (var sortField in WidgetQueryFixture.SortableFields)
        {
            var propertyName = MemberName(sortField.Value);
            Assert.Contains(entity.GetIndexes(), index =>
                index.Properties.Select(property => property.Name)
                    .SequenceEqual(["TenantId", propertyName, "Id"]));
        }

        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT indexdef FROM pg_indexes WHERE schemaname = 'tenant' AND tablename = 'Widgets'
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var definitions = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            definitions.Add(reader.GetString(0));
        }

        Assert.Contains(definitions, definition => definition.Contains("\"TenantId\", \"Name\", \"Id\"", StringComparison.Ordinal));
        Assert.Contains(definitions, definition => definition.Contains("\"TenantId\", \"CreatedAtUtc\", \"Id\"", StringComparison.Ordinal));
        Assert.Contains(definitions, definition => definition.Contains("USING gin", StringComparison.OrdinalIgnoreCase)
            && definition.Contains("f_unaccent", StringComparison.Ordinal)
            && definition.Contains("\"Name\"", StringComparison.Ordinal)
            && definition.Contains("gin_trgm_ops", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Every_sort_field_of_the_readers_has_its_index_with_id_tie_breaker()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = factory.Services.CreateAsyncScope();
        var model = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Model;
        var maps = ReaderSortMaps().ToArray();

        // Cada SortMap es un campo estático, que este test encuentra: ninguno se arma dentro de un método.
        Assert.True(maps.Length >= 10, "Los lectores declaran al menos diez SortMap.");
        Assert.Empty(Directory.EnumerateFiles(InfrastructureDirectory(), "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("new SortMap<", StringComparison.Ordinal))
            .Select(path => Path.GetFileName(path)));

        var fields = maps.SelectMany(map => map.Fields.Select(field => (Map: map, Field: field.Key, Member: field.Value))).ToArray();
        Assert.Equal(Projected.Select(field => $"{field.Map}.{field.Field}").Order(StringComparer.Ordinal),
            fields.Where(field => model.FindEntityType(field.Map.RowType) is null)
                .Select(field => $"{field.Map.Name}.{field.Field}").Order(StringComparer.Ordinal));

        var columns = fields.Where(field => model.FindEntityType(field.Map.RowType) is not null)
            .Select(field => Column(model, field.Map, field.Member))
            .Concat(Projected.Select(field => field.Column).OfType<SortedColumn>())
            .Distinct().ToArray();
        var indexes = await BtreeIndexesAsync(cancellationToken);
        Assert.Empty(columns.Where(column => !indexes.Any(index => Supports(index, column)))
            .Select(column => $"{column.Schema}.{column.Table}.{column.Column}"));
    }

    // El orden de cada campo de una tabla llega a PostgreSQL en los dos sentidos: el «es nulo» de lo vacío y el CASE del
    // circuito de un estado guardado como texto (decisión 264) se traducen y corren.
    [Fact]
    public async Task Every_sort_field_of_a_table_reader_runs_in_both_directions()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var run = typeof(SortIndexTests).GetMethod(nameof(RunEverySortAsync), BindingFlags.Static | BindingFlags.NonPublic)!;
        var maps = (from type in typeof(ApplicationDbContext).Assembly.GetTypes()
                    where !type.ContainsGenericParameters
                    from field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    where field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(SortMap<>)
                    let rowType = field.FieldType.GetGenericArguments()[0]
                    where context.Model.FindEntityType(rowType) is not null
                    select (RowType: rowType, Map: field.GetValue(null)!)).ToArray();

        Assert.NotEmpty(maps);
        foreach (var (rowType, map) in maps)
        {
            await (Task)run.MakeGenericMethod(rowType).Invoke(null, [context, map, cancellationToken])!;
        }
    }

    private static async Task RunEverySortAsync<T>(ApplicationDbContext context, SortMap<T> map, CancellationToken cancellationToken)
        where T : class
    {
        foreach (var field in map.Keys)
        {
            foreach (var descending in new[] { false, true })
            {
                await context.Set<T>().AsNoTracking()
                    .ApplySort(new SortDescriptor(field, descending), map, new SortDescriptor(map.Keys.First(), false),
                        row => EF.Property<Guid>(row, "Id"))
                    .Take(1).ToListAsync(cancellationToken);
            }
        }
    }

    // Privada: (TenantId, campo, Id). Global: termina en (campo, Id).
    private static bool Supports(BtreeIndex index, SortedColumn column) =>
        index.Schema == column.Schema && index.Table == column.Table && (column.Schema == Schemas.Tenant
            ? index.Columns.SequenceEqual(["TenantId", column.Column, "Id"])
            : index.Columns.Length >= 2 && index.Columns[^2] == column.Column && index.Columns[^1] == "Id");

    private static SortedColumn Column(IModel model, SortMapField map, LambdaExpression member)
    {
        var entity = model.FindEntityType(map.RowType)!;
        var property = entity.FindProperty(MemberName(member))
            ?? throw new InvalidOperationException($"{map.Name}: {MemberName(member)} is not a mapped column.");
        var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
        return new SortedColumn(entity.GetSchema()!, entity.GetTableName()!, property.GetColumnName(table)!);
    }

    private static string MemberName(LambdaExpression expression)
    {
        var body = expression.Body is UnaryExpression unary ? unary.Operand : expression.Body;
        return Assert.IsAssignableFrom<MemberExpression>(body).Member.Name;
    }

    // Los campos estáticos SortMap<T> de Infrastructure, con su lector y el tipo que ordenan.
    private static IEnumerable<SortMapField> ReaderSortMaps() =>
        from type in typeof(ApplicationDbContext).Assembly.GetTypes()
        where !type.ContainsGenericParameters
        from field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
        where field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(SortMap<>)
        let map = (IDictionary)field.GetValue(null)!
        select new SortMapField($"{type.Name}.{field.Name}", field.FieldType.GetGenericArguments()[0],
            map.Keys.Cast<string>().ToDictionary(key => key, key => (LambdaExpression)map[key]!, StringComparer.Ordinal));

    private async Task<List<BtreeIndex>> BtreeIndexesAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT schemaname, tablename, indexdef FROM pg_indexes
            WHERE schemaname IN ('tenant', 'platform', 'identity', 'public_site', 'engagement')
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var indexes = new List<BtreeIndex>();
        while (await reader.ReadAsync(cancellationToken))
        {
            if (BtreeColumns.Match(reader.GetString(2)) is not { Success: true } match) continue;
            indexes.Add(new BtreeIndex(reader.GetString(0), reader.GetString(1), [.. match.Groups["columns"].Value
                .Split(',', StringSplitOptions.TrimEntries).Select(column => column.Trim('"'))]));
        }
        return indexes;
    }

    private static string InfrastructureDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ArquitecturaBaseMultitenant.slnx")))
            directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("ArquitecturaBaseMultitenant.slnx was not found."),
            "src", "ArquitecturaBaseMultitenant.Infrastructure");
    }

    private sealed record SortMapField(string Name, Type RowType, IReadOnlyDictionary<string, LambdaExpression> Fields);

    private sealed record SortedColumn(string Schema, string Table, string Column);

    private sealed record ProjectedSortField(string Map, string Field, SortedColumn? Column);

    private sealed record BtreeIndex(string Schema, string Table, string[] Columns);
}
