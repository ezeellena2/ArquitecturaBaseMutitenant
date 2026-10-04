using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Models.Platform;

/// <summary>
/// Estado que ve Plataforma. NeedsRecovery se deriva: cuenta Active sin ningún método que sirva para ingresar;
/// los demás estados de la cuenta tienen prioridad.
/// </summary>
public enum PlatformAccountState { Active, Suspended, NeedsRecovery, PendingDeletion, Deleted }

public enum PlatformAccountKind { Person, Operator }

public sealed record ListPlatformAccountsRequest : PagedRequest
{
    public static readonly IReadOnlyCollection<string> SortableFields = ["name"];
    public PlatformAccountState? State { get; init; }
    public PlatformAccountKind? Kind { get; init; }
}

/// <summary>Fila del listado: el correo principal completo identifica la cuenta, como en el tablero Cuentas.</summary>
public sealed record PlatformAccountResponse(Guid Id, string? DisplayName, Email? PrimaryEmail,
    PlatformAccountKind Kind, PlatformAccountState State, bool HasPersonalSpace, int OrganizationCount,
    DateTime? LastSignInAtUtc, LoginAuditMethod? LastSignInMethod, uint Version);

public sealed record PlatformAccountStateCount(PlatformAccountState State, int Count);
public sealed record PlatformAccountKindCount(PlatformAccountKind Kind, int Count);
public sealed record PlatformAccountFilterCounts(int Total, IReadOnlyList<PlatformAccountStateCount> States,
    IReadOnlyList<PlatformAccountKindCount> Kinds);

/// <summary>Ficha: métodos enmascarados, membresías desde el índice global y permisos si es operador.</summary>
public sealed record PlatformAccountDetailResponse(Guid Id, string? DisplayName, PlatformAccountKind Kind,
    PlatformAccountState State, DateTime? CreatedAtUtc, DateTime? LastSignInAtUtc, LoginAuditMethod? LastSignInMethod,
    bool HasPersonalSpace, PlatformAccountDeletionResponse? Deletion,
    IReadOnlyList<PlatformAccountLoginMethodResponse> LoginMethods,
    IReadOnlyList<PlatformAccountMembershipResponse> Organizations, IReadOnlyList<string> SoleOwnerOrganizations,
    IReadOnlyList<string>? OperatorPermissions, uint Version);

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
    LoginAuditMethod? LastSignInMethod, uint Version);

public sealed record PlatformAccountDetailRow(PlatformAccountRow Account, DateTime? CreatedAtUtc,
    DateTime? DeletionRequestedAtUtc, DateTime? DeletionScheduledForUtc, bool DeletionRequestedByPlatform,
    IReadOnlyList<PlatformAccountMembershipResponse> Organizations, IReadOnlyList<string>? OperatorPermissions);
