using System.Collections;
using System.Reflection;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// persistencia-ef.md, «Nombres»: en los <c>I*Repository</c> y los <c>I*Reader</c> de <c>Interfaces/Persistence</c>,
/// del núcleo y de los módulos, el prefijo dice qué vuelve. <c>Find…</c> da una proyección, un registro o un escalar;
/// <c>List…</c>, una colección o una página; <c>Exists…</c>, un <c>bool</c>, y <c>Count…</c>, un número o un registro
/// de conteos. Una colección nunca sale de un <c>Find…</c> ni un diccionario de un <c>Count…</c>, tampoco en un
/// repositorio, donde <c>Get…</c> da la entidad seguida por EF: es un <c>List…</c>. Un reader no tiene <c>Get…</c>.
/// Los demás verbos (<c>Lock…</c>, <c>Add</c>, <c>Remove</c>) no se miran. Los puertos de <c>Interfaces/Integrations</c>
/// (<c>IUserDisplayNameReader</c>) no son persistencia y quedan afuera.
/// </summary>
public sealed class PersistenceNamingTests
{
    private static readonly Assembly Application = Assembly.Load("ArquitecturaBaseMultitenant.Application");

    [Fact]
    public void Persistence_methods_return_what_their_prefix_says()
    {
        var ports = Application.GetTypes().Where(IsPersistencePort).ToArray();

        Assert.Contains(ports, type => type.Name == "IRoleReader");
        Assert.Contains(ports, type => type.Name == "IRoleRepository");
        Assert.Contains(ports, type => type.Name == "IWhatsAppContactReader");
        Assert.Contains(ports, type => type.Name == "IAssistantSettingsReader");
        Assert.Contains(ports, type => type.Name == "IFormCatalogReader");
        Assert.DoesNotContain(ports, type => type.Name == "IUserDisplayNameReader");

        var violations = ports
            .SelectMany(type => type.GetMethods().Select(method => (Type: type, Method: method)))
            .Select(item => Violation(item.Type, item.Method) is { } reason
                ? $"{item.Type.Name}.{item.Method.Name}: {reason}"
                : null)
            .OfType<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
    }

    [Theory]
    [InlineData(typeof(IWidgetReader), nameof(IWidgetReader.FindByIdAsync), false)]
    [InlineData(typeof(IWidgetReader), nameof(IWidgetReader.FindNameAsync), false)]
    [InlineData(typeof(IWidgetReader), nameof(IWidgetReader.FindAllAsync), true)]
    [InlineData(typeof(IWidgetReader), nameof(IWidgetReader.FindByOwnerAsync), true)]
    [InlineData(typeof(IWidgetReader), nameof(IWidgetReader.ListAsync), false)]
    [InlineData(typeof(IWidgetReader), nameof(IWidgetReader.ListByIdsAsync), false)]
    [InlineData(typeof(IWidgetReader), nameof(IWidgetReader.ListNamesByIdAsync), false)]
    [InlineData(typeof(IWidgetReader), nameof(IWidgetReader.ListCursorAsync), false)]
    [InlineData(typeof(IWidgetReader), nameof(IWidgetReader.ListSummaryAsync), false)]
    [InlineData(typeof(IWidgetReader), nameof(IWidgetReader.ListOneAsync), true)]
    [InlineData(typeof(IWidgetReader), nameof(IWidgetReader.ExistsAsync), false)]
    [InlineData(typeof(IWidgetReader), nameof(IWidgetReader.ExistsCountAsync), true)]
    [InlineData(typeof(IWidgetReader), nameof(IWidgetReader.CountAsync), false)]
    [InlineData(typeof(IWidgetReader), nameof(IWidgetReader.CountFiltersAsync), false)]
    [InlineData(typeof(IWidgetReader), nameof(IWidgetReader.CountByOwnerAsync), true)]
    [InlineData(typeof(IWidgetReader), nameof(IWidgetReader.GetFilterCountsAsync), true)]
    [InlineData(typeof(IWidgetRepository), nameof(IWidgetRepository.GetAsync), false)]
    [InlineData(typeof(IWidgetRepository), nameof(IWidgetRepository.GetByOwnerAsync), true)]
    [InlineData(typeof(IWidgetRepository), nameof(IWidgetRepository.FindByOwnerAsync), true)]
    [InlineData(typeof(IWidgetRepository), nameof(IWidgetRepository.CountByOwnerAsync), true)]
    [InlineData(typeof(IWidgetRepository), nameof(IWidgetRepository.LockAsync), false)]
    public void Detector_finds_each_wrong_prefix_and_keeps_the_right_ones(Type port, string method, bool wrong) =>
        Assert.Equal(wrong, Violation(port, port.GetMethod(method)!) is not null);

    private static bool IsPersistencePort(Type type) =>
        type.IsInterface
        && type.Namespace is { } ns
        && ns.EndsWith(".Interfaces.Persistence", StringComparison.Ordinal)
        && (type.Name.EndsWith("Repository", StringComparison.Ordinal)
            || type.Name.EndsWith("Reader", StringComparison.Ordinal));

    private static string? Violation(Type port, MethodInfo method)
    {
        var name = method.Name;
        var result = Unwrap(method.ReturnType);
        var reader = port.Name.EndsWith("Reader", StringComparison.Ordinal);

        if (HasPrefix(name, "Find"))
            return IsCollection(result) ? "un Find… no devuelve una colección: es un List…" : null;
        if (HasPrefix(name, "List"))
            return IsCollection(result) || IsPage(result) ? null : "un List… devuelve una colección o una página";
        if (HasPrefix(name, "Exists"))
            return result == typeof(bool) ? null : "un Exists… devuelve un bool";
        if (HasPrefix(name, "Count"))
            return IsCollection(result) || result == typeof(bool) || result == typeof(string)
                ? "un Count… devuelve un número o un registro de conteos; un diccionario es un List…"
                : null;
        if (HasPrefix(name, "Get"))
        {
            if (reader)
                return "un reader lee con Find…, List…, Exists… o Count…; Get… es del repositorio";
            return IsCollection(result) ? "un Get… devuelve la entidad o null: una colección es un List…" : null;
        }

        return null;
    }

    private static bool HasPrefix(string name, string prefix) =>
        name.StartsWith(prefix, StringComparison.Ordinal)
        && (name.Length == prefix.Length || !char.IsLower(name[prefix.Length]));

    private static Type Unwrap(Type type)
    {
        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            if (definition == typeof(Task<>) || definition == typeof(ValueTask<>))
                return type.GetGenericArguments()[0];
        }

        return type;
    }

    private static bool IsCollection(Type type) =>
        type != typeof(string)
        && (typeof(IEnumerable).IsAssignableFrom(type)
            || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>)));

    // Una página es el registro que lleva la colección con sus totales o su cursor: PagedResult, CursorResult o uno
    // propio con sus filas (una página dentro de otro registro también cuenta).
    private static bool IsPage(Type type) => IsPage(type, depth: 0);

    private static bool IsPage(Type type, int depth) =>
        depth < 2
        && !IsCollection(type)
        && type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Any(property => IsCollection(property.PropertyType) || IsPage(property.PropertyType, depth + 1));

    // Controles: un prefijo con lo que no le toca, que el detector tiene que ver, y cada prefijo con lo suyo.
    private sealed record Widget(Guid Id, string Name);

    private sealed record WidgetCounts(int Active, int Inactive);

    private sealed record WidgetSummary(int Total, PagedResult<Widget> Rows);

    private interface IWidgetReader
    {
        Task<Widget?> FindByIdAsync(Guid id, CancellationToken ct);

        Task<string?> FindNameAsync(Guid id, CancellationToken ct);

        Task<IReadOnlyList<Widget>> FindAllAsync(CancellationToken ct);

        Task<IReadOnlySet<Guid>> FindByOwnerAsync(Guid ownerId, CancellationToken ct);

        Task<PagedResult<Widget>> ListAsync(CancellationToken ct);

        Task<IReadOnlyList<Widget>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

        Task<IReadOnlyDictionary<Guid, string>> ListNamesByIdAsync(CancellationToken ct);

        Task<CursorResult<Widget>> ListCursorAsync(CancellationToken ct);

        Task<WidgetSummary> ListSummaryAsync(CancellationToken ct);

        Task<Widget?> ListOneAsync(CancellationToken ct);

        Task<bool> ExistsAsync(Guid id, CancellationToken ct);

        Task<int> ExistsCountAsync(Guid id, CancellationToken ct);

        Task<int> CountAsync(CancellationToken ct);

        Task<WidgetCounts> CountFiltersAsync(string? search, CancellationToken ct);

        Task<IReadOnlyDictionary<Guid, int>> CountByOwnerAsync(CancellationToken ct);

        Task<WidgetCounts> GetFilterCountsAsync(string? search, CancellationToken ct);
    }

    private interface IWidgetRepository
    {
        Task<Widget?> GetAsync(Guid id, CancellationToken ct);

        Task<List<Widget>> GetByOwnerAsync(Guid ownerId, CancellationToken ct);

        Task<IReadOnlyList<Widget>> FindByOwnerAsync(Guid ownerId, CancellationToken ct);

        Task<Dictionary<Guid, int>> CountByOwnerAsync(CancellationToken ct);

        Task LockAsync(Guid id, CancellationToken ct);
    }
}
