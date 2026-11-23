using System.Reflection;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// capas-y-flujo.md: sin repositorio genérico. Cada repositorio lleva el nombre de su agregado (<c>IRoleRepository</c>):
/// ningún tipo de producción se llama <c>IRepository</c>, <c>Repository</c>, <c>GenericRepository</c> o
/// <c>RepositoryBase</c>, ninguno que termine en <c>Repository</c> es genérico (<c>EfRepository&lt;T&gt;</c>), y ninguna
/// interfaz de repositorio expone un método genérico (<c>AddAsync&lt;TEntity&gt;</c>).
/// </summary>
public sealed class GenericRepositoryTests
{
    private static readonly string[] GenericNames = ["Repository", "GenericRepository", "RepositoryBase", "BaseRepository"];

    private static readonly Assembly[] Production =
    [
        Assembly.Load("ArquitecturaBaseMultitenant.Domain"),
        Assembly.Load("ArquitecturaBaseMultitenant.Application"),
        Assembly.Load("ArquitecturaBaseMultitenant.Infrastructure"),
        Assembly.Load("ArquitecturaBaseMultitenant.Api"),
    ];

    [Fact]
    public void No_production_type_is_a_generic_repository()
    {
        var types = Production.SelectMany(assembly => assembly.GetTypes()).ToArray();

        Assert.Contains(types, type => type.IsInterface && type.Name == "IRoleRepository");
        Assert.Empty(types.Where(IsGenericRepository).Select(type => type.FullName));
    }

    [Theory]
    [InlineData(typeof(IRepository<>), true)]
    [InlineData(typeof(Repository<>), true)]
    [InlineData(typeof(IGenericRepository), true)]
    [InlineData(typeof(RepositoryBase), true)]
    [InlineData(typeof(IWidgetRepository), true)]
    [InlineData(typeof(EfRepository<,>), true)]
    [InlineData(typeof(IWidgetNamedRepository), false)]
    [InlineData(typeof(IUnitOfWork), false)]
    public void Detector_finds_generic_repositories_and_keeps_the_named_ones(Type type, bool generic) =>
        Assert.Equal(generic, IsGenericRepository(type));

    private static bool IsGenericRepository(Type type)
    {
        var name = BareName(type);
        if (GenericNames.Contains(type.IsInterface && name.StartsWith('I') ? name[1..] : name, StringComparer.Ordinal))
            return true;

        if (!name.EndsWith("Repository", StringComparison.Ordinal))
            return false;

        return type.IsGenericTypeDefinition
            || (type.IsInterface && type.GetMethods().Any(method => method.IsGenericMethodDefinition));
    }

    private static string BareName(Type type) => type.Name.Split('`')[0];

    // Controles: formas de un repositorio genérico que el detector tiene que ver, y una con nombre que tiene que dejar.
    private interface IRepository<T>;

    private abstract class Repository<T>;

    private interface IGenericRepository;

    private abstract class RepositoryBase;

    private interface IWidgetRepository
    {
        Task AddAsync<TEntity>(TEntity entity, CancellationToken ct);
    }

    private abstract class EfRepository<TEntity, TKey>;

    private interface IWidgetNamedRepository
    {
        Task AddAsync(object widget, CancellationToken ct);
    }
}
