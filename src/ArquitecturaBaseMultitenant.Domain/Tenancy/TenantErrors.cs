using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Tenancy;

/// <summary>
/// Centraliza los errores de acceso por el estado de una organización y los cambios de estado no
/// permitidos. Cada código conserva el motivo para la API y las traducciones.
/// </summary>
public static class TenantErrors
{
    public const string NotFoundCode = "Tenancy.Tenant.NotFound";
    public static readonly Error NotFound = Error.NotFound(NotFoundCode, "The organization was not found.");
    public const string SuspendedCode = "Tenancy.Tenant.Suspended";
    public const string PendingApprovalCode = "Tenancy.Tenant.PendingApproval";
    public const string ProvisioningCode = "Tenancy.Tenant.Provisioning";
    public const string ClosedCode = "Tenancy.Tenant.Closed";
    public const string InvalidTransitionCode = "Tenancy.Tenant.InvalidTransition";
    public const string ProvisioningConflictCode = "Tenancy.Tenant.ProvisioningConflict";
    public const string NameRequiredCode = "Tenancy.Tenant.NameRequired";
    public const string NameTooLongCode = "Tenancy.Tenant.NameTooLong";

    public static readonly Error Suspended = Error.Forbidden(SuspendedCode, "The organization is suspended.");
    public static readonly Error PendingApproval = Error.Forbidden(PendingApprovalCode, "The organization is awaiting approval.");
    public static readonly Error Provisioning = Error.Forbidden(ProvisioningCode, "The organization is being provisioned.");
    public static readonly Error Closed = Error.Forbidden(ClosedCode, "The organization is closed.");
    public static readonly Error InvalidTransition = Error.Conflict(InvalidTransitionCode, "The organization cannot change to that state.");
    public static readonly Error ProvisioningConflict = Error.Conflict(ProvisioningConflictCode,
        "The organization already exists with different provisioning data.");
    public static readonly Error NameRequired = Error.Validation(NameRequiredCode, "An organization name is required.");
    public static readonly Error NameTooLong = Error.Validation(NameTooLongCode, "The organization name is too long.");
}
