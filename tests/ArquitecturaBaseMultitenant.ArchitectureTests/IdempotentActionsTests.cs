using System.Reflection;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

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

    private static bool NeedsIdempotency(MethodInfo method) =>
        method.IsDefined(typeof(HttpPostAttribute), inherit: true)
        && method.GetCustomAttributes<ProducesResponseTypeAttribute>(inherit: true)
            .Any(attribute => attribute.StatusCode is 201 or 202);

    private sealed class FixtureController : ControllerBase
    {
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public static StatusCodeResult Create() => new(StatusCodes.Status201Created);

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        public static StatusCodeResult Send() => new(StatusCodes.Status202Accepted);
    }
}
