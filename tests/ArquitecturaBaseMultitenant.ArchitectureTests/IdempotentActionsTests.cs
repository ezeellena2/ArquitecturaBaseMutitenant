using System.Reflection;
using System.Runtime.CompilerServices;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Idempotency;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Mono.Cecil;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// Detecta acciones POST que crean o envían sin declarar idempotencia. Protege las operaciones frente a
/// pedidos repetidos. Desde la E9, una acción en modo durable declara además su verificador concreto: sin él, el filtro
/// la dejaría pasar sin reserva ni replay.
/// </summary>
public sealed class IdempotentActionsTests
{
    [Fact]
    public void Creating_or_sending_posts_declare_idempotency()
    {
        var violations = typeof(ControllerResultExtensions).Assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(ControllerBase)))
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            .Where(NeedsIdempotency)
            .Where(method => !method.IsDefined(typeof(IdempotentAttribute), inherit: true))
            .Select(method => $"{method.DeclaringType?.Name}.{method.Name}");

        Assert.Empty(violations);
    }

    [Fact]
    public void A_post_declaring_201_or_202_without_the_marker_is_detected()
    {
        Assert.True(NeedsIdempotency(typeof(FixtureController).GetMethod(nameof(FixtureController.Create))!));
        Assert.True(NeedsIdempotency(typeof(FixtureController).GetMethod(nameof(FixtureController.Send))!));
        Assert.False(NeedsIdempotency(typeof(FixtureController).GetMethod(nameof(FixtureController.ToString))!));
    }

    [Fact]
    public void A_post_using_created_or_accepted_result_without_status_metadata_is_detected()
    {
        Assert.True(NeedsIdempotency(typeof(FixtureController).GetMethod(nameof(FixtureController.CreateViaResult))!));
        Assert.True(NeedsIdempotency(typeof(FixtureController).GetMethod(nameof(FixtureController.SendViaResult))!));
    }

    [Fact]
    public void Durable_posts_declare_a_concrete_verifier()
    {
        var violations = typeof(ControllerResultExtensions).Assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(ControllerBase)))
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            .Where(method => !DeclaresDurableVerifier(method))
            .Select(method => $"{method.DeclaringType?.Name}.{method.Name}");

        Assert.Empty(violations);
    }

    [Fact]
    public void A_durable_post_without_a_concrete_verifier_is_detected()
    {
        Assert.False(DeclaresDurableVerifier(typeof(FixtureController).GetMethod(nameof(FixtureController.DurableWithoutVerifier))!));
        Assert.False(DeclaresDurableVerifier(typeof(FixtureController).GetMethod(nameof(FixtureController.DurableWithInterfaceVerifier))!));
        Assert.True(DeclaresDurableVerifier(typeof(FixtureController).GetMethod(nameof(FixtureController.DurableWithVerifier))!));
        Assert.True(DeclaresDurableVerifier(typeof(FixtureController).GetMethod(nameof(FixtureController.CreateViaResult))!));
    }

    private static bool DeclaresDurableVerifier(MethodInfo method) =>
        method.GetCustomAttribute<IdempotentAttribute>(inherit: true)?.Mode
            is not (IdempotencyMode.DurableReference or IdempotencyMode.DurableStream)
        || method.GetCustomAttribute<IdempotencyDurableAttribute>(inherit: true)?.VerifierType is { IsAbstract: false, IsInterface: false };

    private static bool NeedsIdempotency(MethodInfo method) =>
        method.IsDefined(typeof(HttpPostAttribute), inherit: true)
        && (method.GetCustomAttributes<ProducesResponseTypeAttribute>(inherit: true)
            .Any(attribute => attribute.StatusCode is 201 or 202)
            || CallsCreationResult(method));

    private static bool CallsCreationResult(MethodInfo method)
    {
        using var module = ModuleDefinition.ReadModule(method.Module.FullyQualifiedName);
        var methods = new List<MethodInfo> { method };
        var moveNext = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
            .GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (moveNext is not null)
        {
            methods.Add(moveNext);
        }

        return methods
            .Select(candidate => module.LookupToken(candidate.MetadataToken))
            .OfType<MethodDefinition>()
            .Where(candidate => candidate.HasBody)
            .SelectMany(candidate => candidate.Body.Instructions)
            .Any(instruction => instruction.Operand is MethodReference called
                && called.DeclaringType.FullName == typeof(ControllerResultExtensions).FullName
                && called.Name is nameof(ControllerResultExtensions.ToCreatedResult)
                    or nameof(ControllerResultExtensions.ToAcceptedResult));
    }

    private sealed class FixtureController : ControllerBase
    {
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public static StatusCodeResult Create() => new(StatusCodes.Status201Created);

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        public static StatusCodeResult Send() => new(StatusCodes.Status202Accepted);

        [HttpPost]
        public IActionResult CreateViaResult() =>
            Result.Success(1).ToCreatedResult(this, nameof(Create), id => new { id });

        [HttpPost]
        public IActionResult SendViaResult() => Result.Success().ToAcceptedResult(this);

        [HttpPost]
        [Idempotent(Mode = IdempotencyMode.DurableStream)]
        public static StatusCodeResult DurableWithoutVerifier() => new(StatusCodes.Status200OK);

        [HttpPost]
        [Idempotent(Mode = IdempotencyMode.DurableReference)]
        [IdempotencyDurable(typeof(IIdempotentRequestVerifier))]
        public static StatusCodeResult DurableWithInterfaceVerifier() => new(StatusCodes.Status201Created);

        [HttpPost]
        [Idempotent(Mode = IdempotencyMode.DurableReference)]
        [IdempotencyDurable(typeof(PlatformFactorDurableAdapter))]
        public static StatusCodeResult DurableWithVerifier() => new(StatusCodes.Status201Created);
    }
}
