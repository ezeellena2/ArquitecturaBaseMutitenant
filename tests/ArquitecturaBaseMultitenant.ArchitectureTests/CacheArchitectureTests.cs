using System.Reflection;
using Mono.Cecil;
using NetArchTest.Rules;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// ADR 0052 y docs/rules/cache.md: Redis es la única caché y se usa sólo detrás de <c>Infrastructure/Caching</c>. Cada
/// lectura pasa por <c>SharedCache</c> desde una caché del inventario cerrado, guarda DTO y nunca entidades, y la tabla de
/// generaciones tiene consumidores exactos.
/// </summary>
public sealed class CacheArchitectureTests
{
    private const string Caching = "ArquitecturaBaseMultitenant.Infrastructure.Caching";
    private const string SharedCache = Caching + ".SharedCache";
    private const string OwnScopeFill = Caching + ".HybridCacheExtensions";

    private static readonly Assembly[] Production =
    [
        Assembly.Load("ArquitecturaBaseMultitenant.Domain"),
        Assembly.Load("ArquitecturaBaseMultitenant.Application"),
        Assembly.Load("ArquitecturaBaseMultitenant.Infrastructure"),
        Assembly.Load("ArquitecturaBaseMultitenant.Api"),
    ];

    private static readonly Lazy<ModuleDefinition[]> Modules = new(() =>
    {
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(AppContext.BaseDirectory);
        return [.. Production.Select(assembly => ModuleDefinition.ReadModule(assembly.Location,
            new ReaderParameters { AssemblyResolver = resolver }))];
    });

    // El inventario cerrado de ADR 0052. Una caché nueva se agrega acá, en la ADR y en la ficha.
    private static readonly HashSet<string> Inventory = new(StringComparer.Ordinal)
    {
        Caching + ".TenantStatusCache",
        Caching + ".AccessStatusCache",
        Caching + ".RolePermissionCache",
        Caching + ".TenantFeatureCache",
        "ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers.TenantSettingsReader",
        "ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers.PlatformSettingsReader",
        "ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers.ReferenceDataReader",
    };

    [Fact]
    public void Only_the_caching_adapters_touch_hybrid_cache_or_redis()
    {
        var result = Types.InAssemblies(Production)
            .That().DoNotResideInNamespace(Caching)
            .ShouldNot().HaveDependencyOnAny(
                "Microsoft.Extensions.Caching.Hybrid",
                "Microsoft.Extensions.Caching.Distributed",
                "Microsoft.Extensions.Caching.StackExchangeRedis",
                "StackExchange.Redis")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(Environment.NewLine, result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Every_cached_read_goes_through_shared_cache_from_an_inventory_cache()
    {
        var reads = Calls(SharedCache, "GetOrLoadAsync");
        var fills = Calls(OwnScopeFill, "GetOrCreateInOwnScopeAsync");

        Assert.Equal(Inventory.Order(StringComparer.Ordinal),
            reads.Select(call => call.Owner).Distinct().Order(StringComparer.Ordinal));
        Assert.Equal([SharedCache], fills.Select(call => call.Owner).Distinct());
    }

    [Fact]
    public void Cached_values_are_dtos_never_entities()
    {
        var values = Calls(SharedCache, "GetOrLoadAsync")
            .Select(call => Element(call.Method.GenericArguments[1]))
            .Where(value => value is not GenericParameter)
            .ToArray();

        Assert.NotEmpty(values);
        Assert.Empty(values.Where(IsEntity).Select(value => value.FullName));
        var tenant = Modules.Value[0].GetType(typeof(Domain.Tenancy.Tenant).FullName);
        Assert.True(IsEntity(tenant));
    }

    [Fact]
    public void Only_the_generation_writer_and_reader_use_the_generations_table()
    {
        var source = Path.Combine(SolutionRoot.FullPath, "src");
        var consumers = Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(source, file).Replace('\\', '/'))
            .Where(path => !path.Contains("/obj/", StringComparison.Ordinal) && !path.Contains("/bin/", StringComparison.Ordinal)
                && !path.StartsWith("ArquitecturaBaseMultitenant.Infrastructure/Persistence/Migrations/", StringComparison.Ordinal))
            .Where(path => File.ReadLines(Path.Combine(source, path))
                .Any(line => line.Contains("CacheTagVersion", StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
        [
            "ArquitecturaBaseMultitenant.Infrastructure/Caching/CacheGenerationInterceptor.cs",
            "ArquitecturaBaseMultitenant.Infrastructure/Caching/CacheTagVersion.cs",
            "ArquitecturaBaseMultitenant.Infrastructure/Caching/SharedCache.cs",
            "ArquitecturaBaseMultitenant.Infrastructure/Persistence/ApplicationDbContext.cs",
            "ArquitecturaBaseMultitenant.Infrastructure/Persistence/Configurations/Platform/CacheTagVersionConfiguration.cs",
            "ArquitecturaBaseMultitenant.Infrastructure/Persistence/DatabaseBootstrapExtensions.cs",
            "ArquitecturaBaseMultitenant.Infrastructure/Persistence/Rls/TenantIsolationModelValidator.cs",
        ], consumers);
    }

    private static (string Owner, GenericInstanceMethod Method)[] Calls(string declaringType, string method) =>
    [
        .. Modules.Value.SelectMany(module => module.GetTypes())
            .SelectMany(type => type.Methods.Where(candidate => candidate.HasBody)
                .SelectMany(candidate => candidate.Body.Instructions)
                .Select(instruction => instruction.Operand)
                .OfType<GenericInstanceMethod>()
                .Where(called => called.DeclaringType.FullName == declaringType && called.Name == method)
                .Select(called => (Outer(type).FullName, called))),
    ];

    private static TypeDefinition Outer(TypeDefinition type) => type.DeclaringType is { } outer ? Outer(outer) : type;

    private static TypeReference Element(TypeReference type) => type switch
    {
        ArrayType array => Element(array.ElementType),
        GenericInstanceType generic when generic.ElementType.FullName == "System.Nullable`1" => generic.GenericArguments[0],
        _ => type,
    };

    // Una entidad del dominio, o la cuenta de Identity: nunca se serializa en Redis.
    private static bool IsEntity(TypeReference type)
    {
        for (var current = type; current is not null && !current.FullName.StartsWith("System.", StringComparison.Ordinal);
             current = current.Resolve()?.BaseType)
        {
            if (current.FullName == "ArquitecturaBaseMultitenant.Domain.Common.Entity"
                || current.FullName.StartsWith("Microsoft.AspNetCore.Identity.IdentityUser", StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
