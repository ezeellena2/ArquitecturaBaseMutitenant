using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Tenancy;

public sealed class TenantTests
{
    [Fact]
    public void Business_moves_from_approval_through_provisioning_to_active()
    {
        var tenant = Tenant.CreateBusiness("La Cosecha", requiresApproval: true);
        Assert.Equal(TenantKind.Business, tenant.Kind);
        Assert.Equal(TenantStatus.PendingApproval, tenant.Status);
        Assert.Equal(TenantErrors.PendingApproval, tenant.CanEnter().Error);

        Assert.True(tenant.Approve().IsSuccess);
        Assert.Equal(TenantStatus.Provisioning, tenant.Status);
        Assert.Equal(TenantErrors.Provisioning, tenant.CanEnter().Error);

        Assert.True(tenant.Activate().IsSuccess);
        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.True(tenant.CanEnter().IsSuccess);
    }

    [Fact]
    public void Business_without_approval_starts_provisioning()
    {
        var tenant = Tenant.CreateBusiness("La Cosecha", requiresApproval: false);
        Assert.Equal(TenantStatus.Provisioning, tenant.Status);
        Assert.Equal(TenantErrors.InvalidTransition, tenant.Approve().Error);
    }

    [Fact]
    public void Business_can_suspend_resume_and_close_from_suspended()
    {
        var tenant = Tenant.CreateBusiness("La Cosecha", requiresApproval: false);
        tenant.Activate();

        Assert.True(tenant.Suspend().IsSuccess);
        Assert.Equal(TenantStatus.Suspended, tenant.Status);
        Assert.Equal(TenantErrors.Suspended, tenant.CanEnter().Error);
        Assert.True(tenant.Activate().IsSuccess);
        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.True(tenant.Suspend().IsSuccess);
        Assert.True(tenant.Close().IsSuccess);
        Assert.Equal(TenantStatus.Closed, tenant.Status);
        Assert.Equal(TenantErrors.Closed, tenant.CanEnter().Error);
        Assert.Equal(TenantErrors.InvalidTransition, tenant.Activate().Error);
    }

    [Fact]
    public void Pending_business_can_be_rejected_and_cannot_be_approved_afterward()
    {
        var tenant = Tenant.CreateBusiness("La Cosecha", requiresApproval: true);

        Assert.True(tenant.Close().IsSuccess);
        Assert.Equal(TenantStatus.Closed, tenant.Status);
        Assert.Equal(TenantErrors.InvalidTransition, tenant.Approve().Error);
    }

    [Fact]
    public void Personal_space_is_active_then_closed_without_suspension()
    {
        var tenant = Tenant.CreatePersonal("Ana");
        Assert.Equal(TenantKind.Personal, tenant.Kind);
        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.Equal(TenantErrors.InvalidTransition, tenant.Suspend().Error);
        Assert.True(tenant.Close().IsSuccess);
        Assert.Equal(TenantStatus.Closed, tenant.Status);
    }
}
