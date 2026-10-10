using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Models.Platform;

/// <summary>
/// Estado que ve Plataforma. NeedsRecovery se deriva: cuenta Active sin ningún método que sirva para ingresar;
/// los demás estados de la cuenta tienen prioridad. Invited e InvitationExpired son la invitación de un operador
/// sin aceptar, vigente o vencida (ADR 0042).
/// </summary>
public enum PlatformAccountState { Active, Suspended, NeedsRecovery, PendingDeletion, Deleted, Invited, InvitationExpired }

public enum PlatformAccountKind { Person, Operator }

/// <summary>
/// Suspender, reactivar, cerrar sesiones o dar de baja una cuenta: solo el motivo interno del operador. No lleva
/// versión: la fila de la cuenta cambia con cualquier escritura (un ingreso, los intentos fallidos, el sello de
/// seguridad, el contacto principal) y su <c>xmin</c> daría un 409 falso. La protege el estado de cada transición,
/// dentro de la transacción y con el lock de la cuenta (docs/rules/concurrencia.md).
/// </summary>
public sealed record PlatformAccountReasonRequest(string? Reason)
{
    public override string ToString() => nameof(PlatformAccountReasonRequest);
}

/// <summary>
/// Una acción con motivo sobre una fila que solo cambia por acciones del operador: decidir un pedido de recuperación o
/// quitar a un operador. Lleva la versión vista de esa fila (el pedido o la asignación de permisos), no la de la cuenta.
/// </summary>
public sealed record PlatformAccountActionRequest(uint? Version, string? Reason)
{
    public override string ToString() => nameof(PlatformAccountActionRequest);
}

public sealed record ListPlatformAccountsRequest : PagedRequest
{
    public static readonly IReadOnlyCollection<string> SortableFields = ["name"];
    public PlatformAccountState? State { get; init; }
    public PlatformAccountKind? Kind { get; init; }
}

/// <summary>
/// Fila del listado: el correo principal completo identifica la cuenta, como en el tablero Cuentas. Una invitación de
/// operador sin aceptar lleva su vencimiento (ADR 0042).
/// </summary>
public sealed record PlatformAccountResponse(Guid Id, string? DisplayName, Email? PrimaryEmail,
    PlatformAccountKind Kind, PlatformAccountState State, bool HasPersonalSpace, int OrganizationCount,
    DateTime? LastSignInAtUtc, LoginAuditMethod? LastSignInMethod, DateTime? InvitationExpiresAtUtc)
{
    /// <summary>Phone principal global verificado, enmascarado; null para otro principal o cuenta eliminada.</summary>
    public string? MaskedPrimaryPhone { get; init; }
}

public sealed record PlatformAccountStateCount(PlatformAccountState State, int Count);
public sealed record PlatformAccountKindCount(PlatformAccountKind Kind, int Count);
public sealed record PlatformAccountFilterCounts(int Total, IReadOnlyList<PlatformAccountStateCount> States,
    IReadOnlyList<PlatformAccountKindCount> Kinds);

/// <summary>Ficha: correo principal, métodos enmascarados, membresías globales y permisos si es operador.</summary>
public sealed record PlatformAccountDetailResponse(Guid Id, string? DisplayName, Email? PrimaryEmail, PlatformAccountKind Kind,
    PlatformAccountState State, DateTime? CreatedAtUtc, DateTime? DeletedAtUtc, DateTime? LastSignInAtUtc, LoginAuditMethod? LastSignInMethod,
    DateTime? InvitationExpiresAtUtc, bool HasPersonalSpace, PlatformAccountDeletionResponse? Deletion,
    IReadOnlyList<PlatformAccountLoginMethodResponse> LoginMethods,
    IReadOnlyList<PlatformAccountMembershipResponse> Organizations, IReadOnlyList<string> SoleOwnerOrganizations,
    IReadOnlyList<string>? OperatorPermissions, uint? OperatorVersion, PlatformAccountStateChangeResponse? LastStateChange)
{
    /// <summary>La misma identidad Phone principal enmascarada que el listado, independiente del canal disponible.</summary>
    public string? MaskedPrimaryPhone { get; init; }

    public override string ToString() => nameof(PlatformAccountDetailResponse);
}

/// <summary>Última transición efectiva registrada en SecurityEvents; el motivo desconocido queda null.</summary>
public sealed record PlatformAccountStateChangeResponse(DateTime AtUtc, string Action, string? Reason)
{
    public override string ToString() => nameof(PlatformAccountStateChangeResponse);
}

public sealed record PlatformAccountStateChangeRow(DateTime AtUtc, SecurityEventType Type, string? Reason)
{
    public override string ToString() => nameof(PlatformAccountStateChangeRow);
}

public sealed record PlatformAccountDeletionResponse(DateTime RequestedAtUtc, DateTime ScheduledForUtc,
    bool RequestedByPlatform);

public sealed record PlatformAccountLoginMethodResponse(Guid Id, LoginMethodType Type, string MaskedValue,
    bool IsPrimary, DateTime? VerifiedAtUtc, string? ManagedByOrganizationName, bool CanSignIn);

/// <summary>EndedAtUtc sale del último SecurityEvent de fin de membresía de esa cuenta; sin evidencia queda null.</summary>
public sealed record PlatformAccountMembershipResponse(Guid TenantId, string Name, string? RoleName,
    DateTime? JoinedAtUtc, MemberStatus Status, TenantStatus TenantStatus, DateTime? EndedAtUtc);

/// <summary>Proyección del reader global; el servicio la completa con métodos y textos.</summary>
public sealed record PlatformAccountRow(Guid Id, string? DisplayName, Email? PrimaryEmail, bool IsOperator,
    PlatformAccountState State, bool HasPersonalSpace, int OrganizationCount, DateTime? LastSignInAtUtc,
    LoginAuditMethod? LastSignInMethod, DateTime? InvitationExpiresAtUtc, string? PrimaryPhone)
{
    public override string ToString() => nameof(PlatformAccountRow);
}

public sealed record PlatformAccountDetailRow(PlatformAccountRow Account, DateTime? CreatedAtUtc, DateTime? DeletedAtUtc,
    PlatformAccountStateChangeRow? LastStateChange,
    DateTime? DeletionRequestedAtUtc, DateTime? DeletionScheduledForUtc, bool DeletionRequestedByPlatform,
    IReadOnlyList<PlatformAccountMembershipResponse> Organizations, IReadOnlyList<string>? OperatorPermissions,
    uint? OperatorVersion = null)
{
    public override string ToString() => nameof(PlatformAccountDetailRow);
}
