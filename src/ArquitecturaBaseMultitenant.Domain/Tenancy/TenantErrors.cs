using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Tenancy;

/// <summary>
/// Centraliza los errores de acceso por el estado de una organización y los cambios de estado no
/// permitidos. Cada código conserva el motivo para la API y las traducciones.
/// </summary>
public static class TenantErrors
{
    public const string SuspendedCode = "Tenancy.Tenant.Suspended";
    public const string PendingApprovalCode = "Tenancy.Tenant.PendingApproval";
    public const string ProvisioningCode = "Tenancy.Tenant.Provisioning";
    public const string ClosedCode = "Tenancy.Tenant.Closed";
    public const string InvalidTransitionCode = "Tenancy.Tenant.InvalidTransition";

    public static readonly Error Suspended = Error.Forbidden(SuspendedCode, "The organization is suspended.");
    public static readonly Error PendingApproval = Error.Forbidden(PendingApprovalCode, "The organization is awaiting approval.");
    public static readonly Error Provisioning = Error.Forbidden(ProvisioningCode, "The organization is being provisioned.");
    public static readonly Error Closed = Error.Forbidden(ClosedCode, "The organization is closed.");
    public static readonly Error InvalidTransition = Error.Conflict(InvalidTransitionCode, "The organization cannot change to that state.");
}
