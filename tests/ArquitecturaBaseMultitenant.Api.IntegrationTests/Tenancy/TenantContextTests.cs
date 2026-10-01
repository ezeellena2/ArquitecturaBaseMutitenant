using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

/// <summary>
/// Comprueba entrada, anidación y restauración del contexto de espacio. Exige fallar si se necesita un
/// espacio y no hay acceso activo.
/// </summary>
public sealed class TenantContextTests
{
    [Fact]
    public void Without_an_active_access_required_tenant_throws()
    {
        var context = new TenantContext();

        Assert.Null(context.TenantId);
        Assert.Null(context.TenantKind);
        Assert.Throws<InvalidOperationException>(() => context.RequiredTenantId);
    }

    [Fact]
    public void Enter_restores_previous_access_and_kind_on_dispose()
    {
        var context = new TenantContext();
        var tenantId = Guid.NewGuid();
        context.SetFromAccess(tenantId, TenantKind.Business);

        using (context.Enter(tenantId))
        {
            Assert.Equal(tenantId, context.TenantId);
            Assert.Equal(tenantId, context.RequiredTenantId);
            Assert.Null(context.TenantKind);
        }

        Assert.Equal(tenantId, context.TenantId);
        Assert.Equal(TenantKind.Business, context.TenantKind);
    }

    [Fact]
    public void Enter_without_a_previous_access_restores_empty_context()
    {
        var context = new TenantContext();
        var tenantId = Guid.NewGuid();

        using (context.Enter(tenantId))
        {
            Assert.Equal(tenantId, context.RequiredTenantId);
            Assert.Null(context.TenantKind);
        }

        Assert.Null(context.TenantId);
        Assert.Null(context.TenantKind);
    }

    [Fact]
    public void Nested_scope_for_the_same_tenant_restores_each_level()
    {
        var context = new TenantContext();
        var tenantId = Guid.NewGuid();
        context.SetFromAccess(tenantId, TenantKind.Personal);

        using (context.Enter(tenantId))
        {
            using (context.Enter(tenantId))
            {
                Assert.Equal(tenantId, context.RequiredTenantId);
                Assert.Null(context.TenantKind);
            }

            Assert.Equal(tenantId, context.TenantId);
            Assert.Null(context.TenantKind);
        }

        Assert.Equal(tenantId, context.TenantId);
        Assert.Equal(TenantKind.Personal, context.TenantKind);
    }

    [Fact]
    public void Enter_rejects_an_incompatible_nested_tenant_without_mutation()
    {
        var context = new TenantContext();
        var original = Guid.NewGuid();
        var other = Guid.NewGuid();

        using (context.Enter(original))
        {
            Assert.Throws<InvalidOperationException>(() => context.Enter(other));
            Assert.Equal(original, context.RequiredTenantId);
        }

        Assert.Null(context.TenantId);
    }

    [Fact]
    public void Enter_rejects_switching_away_from_the_authenticated_tenant()
    {
        var context = new TenantContext();
        var original = Guid.NewGuid();
        context.SetFromAccess(original, TenantKind.Business);

        Assert.Throws<InvalidOperationException>(() => context.Enter(Guid.NewGuid()));

        Assert.Equal(original, context.TenantId);
        Assert.Equal(TenantKind.Business, context.TenantKind);
    }

    [Fact]
    public void Enter_rejects_empty_id_and_open_transaction_before_mutation()
    {
        var transactionActive = false;
        var context = new TenantContext(() => transactionActive);

        Assert.Throws<ArgumentException>(() => context.Enter(Guid.Empty));
        transactionActive = true;
        Assert.Throws<InvalidOperationException>(() => context.Enter(Guid.NewGuid()));

        Assert.Null(context.TenantId);
        Assert.Null(context.TenantKind);
    }

    [Fact]
    public void Scope_restores_context_when_work_throws()
    {
        var context = new TenantContext();
        var tenantId = Guid.NewGuid();

        Action work = () =>
        {
            using (context.Enter(tenantId))
            {
                throw new InvalidOperationException("work failed");
            }
        };
        Assert.Throws<InvalidOperationException>(work);

        Assert.Null(context.TenantId);
        Assert.Null(context.TenantKind);
    }

    [Fact]
    public void Access_initialization_rejects_rebinding_and_invalid_kind()
    {
        var context = new TenantContext();
        var tenantId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => context.SetFromAccess(Guid.Empty, TenantKind.Business));
        Assert.Throws<ArgumentOutOfRangeException>(() => context.SetFromAccess(tenantId, (TenantKind)42));
        context.SetFromAccess(tenantId, TenantKind.Personal);
        Assert.Throws<InvalidOperationException>(() => context.SetFromAccess(Guid.NewGuid(), TenantKind.Business));

        Assert.Equal(tenantId, context.TenantId);
        Assert.Equal(TenantKind.Personal, context.TenantKind);
    }
}
