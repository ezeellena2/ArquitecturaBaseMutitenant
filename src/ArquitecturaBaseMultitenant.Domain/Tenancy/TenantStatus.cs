namespace ArquitecturaBaseMultitenant.Domain.Tenancy;

public enum TenantStatus
{
    PendingApproval,
    Provisioning,
    Active,
    Suspended,
    Closed,
}
