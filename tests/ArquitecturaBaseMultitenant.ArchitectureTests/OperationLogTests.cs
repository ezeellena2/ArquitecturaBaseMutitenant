using System.Reflection;
using System.Runtime.CompilerServices;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Mono.Cecil;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// Regla 10 de AGENTS (R09): cada método público de un servicio de Application, es decir cada método de su contrato
/// <c>I*Service</c>, corre dentro de <see cref="OperationLog"/>: <c>RunAsync</c> para un <c>Result</c> o
/// <c>RunValueAsync</c> para un contrato que devuelve un valor. Se lee el IL del método, de su máquina de estados y de
/// los métodos privados del mismo servicio en los que delega.
/// </summary>
public sealed class OperationLogTests
{
    private static readonly Assembly ApplicationAssembly = Assembly.Load("ArquitecturaBaseMultitenant.Application");

    [Fact]
    public void Every_public_method_of_an_application_service_runs_inside_operation_log()
    {
        var methods = ApplicationAssembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && ApplicationNamespaceShape.IsServices(type))
            .SelectMany(type => type.GetInterfaces()
                .Where(ApplicationNamespaceShape.IsServiceContract)
                .SelectMany(contract => type.GetInterfaceMap(contract).TargetMethods))
            .Distinct()
            .ToArray();

        Assert.NotEmpty(methods);
        Assert.Empty(methods.Where(method => !CallsOperationLog(method))
            .Select(method => $"{method.DeclaringType!.FullName}.{method.Name}"));
    }

    [Fact]
    public void A_service_method_outside_operation_log_is_detected()
    {
        Assert.False(CallsOperationLog(typeof(UnloggedProbe).GetMethod(nameof(UnloggedProbe.ReadAsync))!));
        Assert.False(CallsOperationLog(typeof(UnloggedProbe).GetMethod(nameof(UnloggedProbe.DelegatedAsync))!));
        Assert.True(CallsOperationLog(typeof(LoggedProbe).GetMethod(nameof(LoggedProbe.ReadAsync))!));
        Assert.True(CallsOperationLog(typeof(LoggedProbe).GetMethod(nameof(LoggedProbe.ReadAwaitedAsync))!));
        Assert.True(CallsOperationLog(typeof(LoggedProbe).GetMethod(nameof(LoggedProbe.DelegatedAsync))!));
    }

    private static bool CallsOperationLog(MethodInfo method)
    {
        using var module = ModuleDefinition.ReadModule(method.Module.FullyQualifiedName);
        return module.LookupToken(method.MetadataToken) is MethodDefinition definition
            && CallsOperationLog(definition, definition.DeclaringType, []);
    }

    private static bool CallsOperationLog(MethodDefinition method, TypeDefinition service, HashSet<MethodDefinition> visited) =>
        visited.Add(method) && Bodies(method).SelectMany(body => body.Body.Instructions)
            .Select(instruction => instruction.Operand).OfType<MethodReference>()
            .Any(called => called.DeclaringType.FullName == typeof(OperationLog).FullName
                    && called.Name is nameof(OperationLog.RunAsync) or nameof(OperationLog.RunValueAsync)
                || called.Resolve() is { IsPrivate: true } own && own.DeclaringType == service
                    && CallsOperationLog(own, service, visited));

    private static IEnumerable<MethodDefinition> Bodies(MethodDefinition method)
    {
        if (method.HasBody) yield return method;
        var stateMachine = method.CustomAttributes
            .FirstOrDefault(attribute => attribute.AttributeType.FullName == typeof(AsyncStateMachineAttribute).FullName)
            ?.ConstructorArguments[0].Value as TypeReference;
        if (stateMachine?.Resolve()?.Methods.FirstOrDefault(candidate => candidate.Name == "MoveNext") is { HasBody: true } moveNext)
            yield return moveNext;
    }

#pragma warning disable CA1822 // Casos de control: se inspecciona su IL.
    private sealed class UnloggedProbe
    {
        public Task<Result> ReadAsync() => Task.FromResult(Result.Success());

        public Task<Result> DelegatedAsync() => ReadCoreAsync();

        private Task<Result> ReadCoreAsync() => ReadAsync();
    }

    private sealed class LoggedProbe
    {
        private static readonly ILogger Logger = NullLogger.Instance;

        public Task<Result> ReadAsync() =>
            OperationLog.RunAsync(Logger, TimeProvider.System, "Probe", () => Task.FromResult(Result.Success()));

        public async Task<Result> ReadAwaitedAsync() =>
            await OperationLog.RunAsync(Logger, TimeProvider.System, "Probe", () => Task.FromResult(Result.Success()));

        public Task<Result> DelegatedAsync() => ChangeAsync(true);

        private Task<Result> ChangeAsync(bool value) =>
            OperationLog.RunAsync(Logger, TimeProvider.System, value ? "Probe" : "Other", () => Task.FromResult(Result.Success()));
    }
#pragma warning restore CA1822
}
