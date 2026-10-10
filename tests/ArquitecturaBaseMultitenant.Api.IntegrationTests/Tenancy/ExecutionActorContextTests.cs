using ArquitecturaBaseMultitenant.Application.Models.Request;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.RequestContext;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Api.RequestContext;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Interceptors;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

public sealed class ExecutionActorContextTests
{
    [Fact]
    public void Actor_is_available_only_inside_its_scope_and_nested_scopes_restore_the_parent()
    {
        var context = new ExecutionActorContext();
        var first = Actor();
        var second = Actor();
        Assert.Null(context.Actor);
        using (context.Enter(first))
        {
            Assert.Same(first, context.Actor);
            using (context.Enter(second)) Assert.Same(second, context.Actor);
            Assert.Same(first, context.Actor);
        }
        Assert.Null(context.Actor);
    }

    [Fact]
    public async Task Parallel_flows_never_overwrite_each_other_or_the_calling_flow()
    {
        var context = new ExecutionActorContext();
        var first = Actor();
        var second = Actor();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        async Task Work(ExecutionActor actor)
        {
            using (context.Enter(actor))
            {
                if (Interlocked.Increment(ref count) == 2) ready.SetResult();
                await ready.Task;
                Assert.Same(actor, context.Actor);
            }
            Assert.Null(context.Actor);
        }
        await Task.WhenAll(Task.Run(() => Work(first), TestContext.Current.CancellationToken),
            Task.Run(() => Work(second), TestContext.Current.CancellationToken));
        Assert.Null(context.Actor);
    }

    [Fact]
    public async Task Errors_and_cancellation_restore_the_context()
    {
        var context = new ExecutionActorContext();
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            using (context.Enter(Actor()))
            {
                Assert.NotNull(context.Actor);
                await Task.Yield();
                throw new OperationCanceledException();
            }
        });
        Assert.Null(context.Actor);
        Action failure = () =>
        {
            using (context.Enter(Actor())) throw new InvalidOperationException();
        };
        Assert.Throws<InvalidOperationException>(failure);
        Assert.Null(context.Actor);
    }

    private static ExecutionActor Actor() => new(Guid.NewGuid(), Access.Platform, null, "es-AR", "America/Argentina/Buenos_Aires");

    [Fact]
    public void Request_scope_bound_tenant_and_open_transaction_reject_entry_without_mutating_identity()
    {
        var http = false;
        var bound = false;
        var transaction = false;
        var context = new ExecutionActorContext(() => http, () => bound, () => transaction);
        http = true;
        Assert.Throws<InvalidOperationException>(() => context.Enter(Actor()));
        http = false;
        bound = true;
        Assert.Throws<InvalidOperationException>(() => context.Enter(Actor()));
        bound = false;
        transaction = true;
        Assert.Throws<InvalidOperationException>(() => context.Enter(Actor()));
        Assert.Null(context.Actor);
    }

    [Fact]
    public void Signed_HTTP_identity_wins_and_background_current_user_and_tenant_share_one_binding()
    {
        var accessor = new HttpContextAccessor();
        using var context = new ExecutionActorContext(() => accessor.HttpContext is not null);
        var user = new CurrentUser(accessor, context);
        var system = new SystemCurrentUser(context);
        var tenant = new TenantContext(executionActor: () => context.Actor);
        var actor = Actor() with { Access = Access.Business, TenantId = Guid.NewGuid(), TenantKind = TenantKind.Business };
        Assert.Null(user.UserId);
        using (context.Enter(actor))
        {
            Assert.Equal(actor.OwnerUserId, user.UserId);
            Assert.Equal(actor.Access, user.Access);
            Assert.Equal(actor.OwnerUserId, system.UserId);
            Assert.Equal(actor.TenantId, tenant.TenantId);
            Assert.Equal(actor.TenantKind, tenant.TenantKind);
            Assert.Throws<InvalidOperationException>(() => tenant.Enter(actor.TenantId!.Value));
            Assert.Throws<InvalidOperationException>(() => tenant.SetFromAccess(actor.TenantId!.Value, TenantKind.Business));
            var signedId = Guid.NewGuid();
            accessor.HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", signedId.ToString()), new Claim("access", "consumer")], "Test")),
            };
            Assert.Equal(signedId, user.UserId);
            Assert.Equal(Access.Consumer, user.Access);
            Assert.Null(context.Actor);
            Assert.Null(system.UserId);
            Assert.Null(tenant.TenantId);
            accessor.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
            Assert.Null(user.UserId);
            Assert.Null(user.Access);
            accessor.HttpContext = null;
            Assert.Equal(actor.OwnerUserId, user.UserId);
        }
        Assert.Null(tenant.TenantId);
        Assert.Null(tenant.TenantKind);
        Assert.Null(system.UserId);
    }

    [Fact]
    public void Incomplete_binding_invalid_access_or_Platform_tenant_are_rejected()
    {
        using var context = new ExecutionActorContext();
        var actor = Actor();
        Assert.Throws<ArgumentException>(() => context.Enter(actor with { OwnerUserId = Guid.Empty }));
        Assert.Throws<ArgumentException>(() => context.Enter(actor with { Access = (Access)99 }));
        Assert.Throws<ArgumentException>(() => context.Enter(actor with { TenantId = Guid.NewGuid() }));
        Assert.Throws<ArgumentException>(() => context.Enter(actor with { Access = Access.Consumer, TenantId = Guid.NewGuid() }));
        Assert.Throws<ArgumentException>(() => context.Enter(actor with { Access = Access.Business, TenantId = Guid.NewGuid(), TenantKind = TenantKind.Personal }));
        Assert.Null(context.Actor);
    }

    [Fact]
    public async Task Disposed_parent_revokes_inherited_child_flow_and_DI_scope_disposal_revokes_actor()
    {
        var context = new ExecutionActorContext();
        var parent = context.Enter(Actor());
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var child = Task.Run(async () =>
        {
            Assert.NotNull(context.Actor);
            ready.SetResult();
            await release.Task;
            Assert.Null(context.Actor);
        }, TestContext.Current.CancellationToken);
        await ready.Task;
        parent.Dispose();
        parent.Dispose();
        release.SetResult();
        await child;
        using (context.Enter(Actor()))
        {
            context.Dispose();
            Assert.Null(context.Actor);
            Assert.Throws<ObjectDisposedException>(() => context.Enter(Actor()));
        }
    }

    [Fact]
    public void Out_of_order_disposal_is_rejected_and_each_level_can_still_be_disposed()
    {
        using var context = new ExecutionActorContext();
        var outer = context.Enter(Actor());
        var inner = context.Enter(Actor());
        Assert.Throws<InvalidOperationException>(outer.Dispose);
        inner.Dispose();
        Assert.NotNull(context.Actor);
        outer.Dispose();
        Assert.Null(context.Actor);
    }
}
