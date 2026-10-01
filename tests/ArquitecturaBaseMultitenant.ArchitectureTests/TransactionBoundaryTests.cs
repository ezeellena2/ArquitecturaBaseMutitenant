using System.CodeDom.Compiler;
using System.Reflection;
using System.Runtime.CompilerServices;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// Comprueba quién abre transacciones y quién guarda el DbContext. Exige que las escrituras pasen por la
/// unidad de trabajo del caso de uso.
/// </summary>
public sealed class TransactionBoundaryTests
{
    private const string UnitOfWorkClass = "ArquitecturaBaseMultitenant.Infrastructure.Persistence.UnitOfWork";
    private const string Registration = "ArquitecturaBaseMultitenant.Infrastructure.Persistence.PersistenceRegistration";
    private const string ReferenceSeed = "ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed.ReferenceDataSeeder";
    private const string DatabaseSeed = "ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed.DatabaseSeeder";
    private const string IdempotencyStore = "ArquitecturaBaseMultitenant.Infrastructure.Idempotency.IdempotencyStore";
    private const string CacheExtensions = "ArquitecturaBaseMultitenant.Infrastructure.Caching.HybridCacheExtensions";
    private const string ApplicationServices = "ArquitecturaBaseMultitenant.Application.Services";
    private const string ServiceContracts = "ArquitecturaBaseMultitenant.Application.Interfaces.Services";

    private static readonly Assembly[] Production =
    [
        Assembly.Load("ArquitecturaBaseMultitenant.Application"),
        Assembly.Load("ArquitecturaBaseMultitenant.Infrastructure"),
        Assembly.Load("ArquitecturaBaseMultitenant.Api"),
    ];

    [Fact]
    public void Only_use_case_services_and_the_two_named_seeds_receive_unit_of_work()
    {
        var receivers = Receivers(Production.SelectMany(assembly => assembly.GetTypes())).ToArray();

        // La lista cerrada admite dos seeds técnicos; ningún otro adaptador abre el límite.
        Assert.Contains(receivers, type => type.FullName == ReferenceSeed);
        Assert.Contains(receivers, type => type.FullName == DatabaseSeed);
        Assert.Empty(receivers.Where(type => !IsService(type) && type.FullName is not (ReferenceSeed or DatabaseSeed))
            .Select(type => type.FullName));

        Assert.Contains(typeof(RogueReceiver), Receivers([typeof(RogueReceiver)]));
        Assert.False(IsService(typeof(RogueReceiver)));
    }

    [Fact]
    public void Only_use_case_services_and_the_two_named_seeds_call_unit_of_work()
    {
        var callers = Production.SelectMany(ArchitectureIl.Calls)
            .Where(call => call.DeclaringType == typeof(IUnitOfWork).FullName
                && call.Method == nameof(IUnitOfWork.ExecuteInTransactionAsync))
            .Select(call => call.Owner)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.Contains(ReferenceSeed, callers);
        Assert.Contains(DatabaseSeed, callers);
        Assert.DoesNotContain(callers, owner => owner is not (ReferenceSeed or DatabaseSeed) && !IsService(owner));

        var control = ArchitectureIl.Calls(typeof(TransactionBoundaryTests).Assembly);
        Assert.Contains(control, call => call.Owner == typeof(TransactionBoundaryTests).FullName
            && call.DeclaringType == typeof(IUnitOfWork).FullName
            && call.Method == nameof(IUnitOfWork.ExecuteInTransactionAsync));
    }

    [Fact]
    public void Only_registration_and_implementation_name_concrete_unit_of_work()
    {
        var owners = Production.SelectMany(ArchitectureIl.TypeUses)
            .Where(use => use.Type == UnitOfWorkClass)
            .Select(use => use.Owner)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.Contains(Registration, owners);
        Assert.Contains(UnitOfWorkClass, owners);
        Assert.Empty(ConcreteUseViolations(owners));
        Assert.Equal(["Rogue"], ConcreteUseViolations(["Rogue"]));
    }

    [Fact]
    public void Only_unit_of_work_saves_the_ef_context()
    {
        var owners = Production.SelectMany(ArchitectureIl.DbContextSaveCalls)
            .Select(call => call.Owner)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.Contains(UnitOfWorkClass, owners);
        Assert.DoesNotContain(owners, owner => owner != UnitOfWorkClass);

        // Detecta también un SaveChangesAsync llamado sobre un contexto con otro nombre.
        Assert.Contains(ArchitectureIl.DbContextSaveCalls(typeof(TransactionBoundaryTests).Assembly),
            call => call.Owner == typeof(TransactionBoundaryTests).FullName
                && call.Method == nameof(DbContext.SaveChangesAsync));
    }

    [Fact]
    public void Only_unit_of_work_and_idempotency_store_open_transactions()
    {
        var owners = Production.SelectMany(ArchitectureIl.Calls)
            .Where(IsTransactionCall)
            .Select(call => call.Owner)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.Contains(UnitOfWorkClass, owners);
        Assert.Contains(IdempotencyStore, owners);
        Assert.DoesNotContain(owners, owner => owner is not (UnitOfWorkClass or IdempotencyStore));

        Assert.True(IsTransactionCall(new ArchitectureIl.Call("Rogue", "System.Data.Common.DbConnection",
            "BeginTransactionAsync", 1, [])));
    }

    [Fact]
    public void Ef_bulk_writes_require_an_explicit_exception()
    {
        var calls = Production.SelectMany(ArchitectureIl.Calls)
            .Where(IsEfBulkWrite)
            .ToArray();

        // E3b: LoginMethod es identidad técnica sin auditoría ni soft-delete.
        // Solo esta operación retira el principal antes del índice parcial inmediato;
        // exige la UoW y la prueba real cubre cambio de principal/rollback.
        var allowed = Assert.Single(calls);
        Assert.Equal("ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories.LoginMethodRepository", allowed.Owner);
        Assert.Equal("ExecuteUpdateAsync", allowed.Method);
        Assert.True(IsEfBulkWrite(new ArchitectureIl.Call("Rogue",
            "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions", "ExecuteDeleteAsync", 2, [])));
    }

    [Fact]
    public void Only_the_own_scope_helper_fills_hybrid_cache()
    {
        var calls = Production.SelectMany(ArchitectureIl.Calls).Where(IsCacheFill).ToArray();
        var owners = calls.Select(call => call.Owner).Distinct(StringComparer.Ordinal).ToArray();

        // El helper abre un scope y una conexión propios antes de leer para llenar HybridCache.
        Assert.Equal([CacheExtensions], owners);
        Assert.Empty(CacheFillViolations(calls));

        var control = ArchitectureIl.Calls(typeof(TransactionBoundaryTests).Assembly)
            .Where(call => call.Owner == typeof(TransactionBoundaryTests).FullName
                && call.OwnerMethod == nameof(RogueCacheFillAsync))
            .ToArray();
        Assert.Contains(control, IsCacheFill);
        Assert.Equal([typeof(TransactionBoundaryTests).FullName!], CacheFillViolations(control));
        Assert.Equal(["Rogue"], CacheFillViolations(
        [
            new ArchitectureIl.Call("Rogue", typeof(HybridCache).FullName!, "GetOrCreateAsync", 5, []),
        ]));
    }

    [Fact]
    public void Unit_of_work_contract_has_one_explicit_save_boundary()
    {
        var method = Assert.Single(typeof(IUnitOfWork).GetMethods());
        Assert.Equal(nameof(IUnitOfWork.ExecuteInTransactionAsync), method.Name);
        Assert.True(method.IsGenericMethodDefinition);
        Assert.Equal(["OnSuccess", "OnAnyResult"], Enum.GetNames<CommitPolicy>());
    }

    private static IEnumerable<Type> Receivers(IEnumerable<Type> types) => types
        .Where(type => type.IsClass && !type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
        .Where(type => type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Any(constructor => constructor.GetParameters()
                .Any(parameter => parameter.ParameterType == typeof(IUnitOfWork)
                    || parameter.ParameterType.FullName == UnitOfWorkClass)));

    private static bool IsService(string owner) =>
        Production.Select(assembly => assembly.GetType(owner)).OfType<Type>().Any(IsService);

    private static bool IsService(Type type) =>
        type.Namespace?.StartsWith(ApplicationServices + ".", StringComparison.Ordinal) == true
        && type.GetInterfaces().Any(contract => contract.Namespace?.StartsWith(
            ServiceContracts, StringComparison.Ordinal) == true);

    private static string[] ConcreteUseViolations(IEnumerable<string> owners) =>
        [.. owners.Where(owner => owner is not (UnitOfWorkClass or Registration))];

    private static bool IsTransactionCall(ArchitectureIl.Call call) =>
        call.Method is "BeginTransaction" or "BeginTransactionAsync"
            or "Commit" or "CommitAsync" or "CommitTransaction" or "CommitTransactionAsync"
            or "Rollback" or "RollbackAsync" or "RollbackTransaction" or "RollbackTransactionAsync"
        && (call.DeclaringType.StartsWith("Microsoft.EntityFrameworkCore.Storage.", StringComparison.Ordinal)
            || call.DeclaringType.StartsWith("Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade", StringComparison.Ordinal)
            || call.DeclaringType.StartsWith("Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions", StringComparison.Ordinal)
            || call.DeclaringType.StartsWith("System.Data.Common.Db", StringComparison.Ordinal)
            || call.DeclaringType.StartsWith("Npgsql.Npgsql", StringComparison.Ordinal));

    private static bool IsEfBulkWrite(ArchitectureIl.Call call) =>
        call.DeclaringType == "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions"
        && call.Method is "ExecuteUpdate" or "ExecuteUpdateAsync" or "ExecuteDelete" or "ExecuteDeleteAsync";

    private static bool IsCacheFill(ArchitectureIl.Call call) =>
        call.DeclaringType == typeof(HybridCache).FullName
        && call.Method is "GetOrCreateAsync" or "SetAsync";

    private static string[] CacheFillViolations(IEnumerable<ArchitectureIl.Call> calls) =>
        [.. calls.Where(IsCacheFill)
            .Where(call => call.Owner != CacheExtensions)
            .Select(call => call.Owner)
            .Distinct(StringComparer.Ordinal)];

#pragma warning disable CA1812 // Casos de control que se leen por reflexión/IL, nunca se instancian.
    private sealed class RogueReceiver(IUnitOfWork unitOfWork)
    {
        public IUnitOfWork UnitOfWork { get; } = unitOfWork;
    }

    private sealed class Ledger : DbContext
    {
        public Task<int> SaveOnceAsync() => SaveChangesAsync();
    }
#pragma warning restore CA1812

    private static Task<Result> RogueCallAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(_ => Task.FromResult(Result.Success()),
            CommitPolicy.OnSuccess, cancellationToken);

    private static ValueTask RogueCacheFillAsync(HybridCache cache) => cache.SetAsync("control", 0);
}

// Se comparte entre los cinco tests de arquitectura de E2. Cada llamada conserva el método dueño y sus literales;
// los tipos anidados generados por async/lambdas se atribuyen a su clase exterior.
internal static class ArchitectureIl
{
    internal sealed record Call(string Owner, string DeclaringType, string Method, int ParameterCount,
        IReadOnlyList<string> Literals, string OwnerMethod = "",
        IReadOnlyList<string>? GenericArguments = null);

    internal sealed record TypeUse(string Owner, string Type);

    internal static IReadOnlyList<Call> Calls(Assembly assembly) => Scan(assembly, (owner, method) =>
    {
        var literals = method.Body.Instructions.OfType<Instruction>()
            .Select(instruction => instruction.Operand)
            .OfType<string>()
            .ToArray();
        return method.Body.Instructions
            .Where(instruction => instruction.Operand is MethodReference)
            .Select(instruction => (MethodReference)instruction.Operand)
            .Select(called => new Call(owner, called.DeclaringType.FullName, called.Name,
                called.Parameters.Count, literals, method.Name,
                called is GenericInstanceMethod generic
                    ? [.. generic.GenericArguments.Select(argument => argument.FullName)]
                    : []));
    });

    internal static IReadOnlyList<Call> DbContextSaveCalls(Assembly assembly) => Scan(assembly, (owner, method) =>
        method.Body.Instructions
            .Where(instruction => instruction.Operand is MethodReference)
            .Select(instruction => (MethodReference)instruction.Operand)
            .Where(IsDbContextSave)
            .Select(called => new Call(owner, called.DeclaringType.FullName, called.Name,
                called.Parameters.Count, [], method.Name)));

    internal static IReadOnlyList<TypeUse> TypeUses(Assembly assembly) => Scan(assembly, (owner, method) =>
        method.Body.Instructions.SelectMany(instruction => NamedTypes(instruction.Operand))
            .Distinct(StringComparer.Ordinal)
            .Select(type => new TypeUse(owner, type)));

    private static IReadOnlyList<T> Scan<T>(Assembly assembly, Func<string, MethodDefinition, IEnumerable<T>> select)
    {
        using var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Path.GetDirectoryName(assembly.Location));
        using var module = ModuleDefinition.ReadModule(assembly.Location,
            new ReaderParameters { AssemblyResolver = resolver });

        return [.. module.GetTypes()
            .Where(type => !IsGenerated(Outermost(type)))
            .SelectMany(type => type.Methods.Where(method => method.HasBody)
                .SelectMany(method => select(Outermost(type).FullName, method)))];
    }

    private static bool IsDbContextSave(MethodReference called)
    {
        if (called.Name is not ("SaveChanges" or "SaveChangesAsync"))
        {
            return false;
        }

        var method = called.Resolve() ?? throw new InvalidOperationException(
            $"Cecil could not resolve {called.FullName}.");
        if (method.DeclaringType.IsInterface)
        {
            return true;
        }

        for (var type = method.DeclaringType; type is not null; type = BaseOf(type))
        {
            if (type.FullName == typeof(DbContext).FullName)
            {
                return true;
            }
        }

        return false;
    }

    private static TypeDefinition? BaseOf(TypeDefinition type) =>
        type.BaseType is not { } baseType || baseType.FullName == typeof(object).FullName
            ? null
            : baseType.Resolve() ?? throw new InvalidOperationException(
                $"Cecil could not resolve {baseType.FullName}.");

    private static IEnumerable<string> NamedTypes(object? operand) => operand switch
    {
        MethodReference method =>
        [
            .. Expand(method.DeclaringType),
            .. Expand(method.ReturnType),
            .. method.Parameters.SelectMany(parameter => Expand(parameter.ParameterType)),
            .. method is GenericInstanceMethod generic ? generic.GenericArguments.SelectMany(Expand) : [],
        ],
        FieldReference field => [.. Expand(field.DeclaringType), .. Expand(field.FieldType)],
        TypeReference type => Expand(type),
        _ => [],
    };

    private static IEnumerable<string> Expand(TypeReference type) => type switch
    {
        GenericInstanceType generic => [generic.ElementType.FullName, .. generic.GenericArguments.SelectMany(Expand)],
        TypeSpecification specification => Expand(specification.ElementType),
        GenericParameter => [],
        _ => [type.FullName],
    };

    private static TypeDefinition Outermost(TypeDefinition type)
    {
        while (type.DeclaringType is not null)
        {
            type = type.DeclaringType;
        }

        return type;
    }

    private static bool IsGenerated(TypeDefinition type) =>
        type.CustomAttributes.Any(attribute =>
            attribute.AttributeType.FullName == typeof(GeneratedCodeAttribute).FullName);
}
