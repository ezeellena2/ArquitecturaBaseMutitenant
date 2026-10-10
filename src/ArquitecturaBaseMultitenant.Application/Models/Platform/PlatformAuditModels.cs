using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Models.Platform;

/// <summary>
/// Plataforma › Auditoría: período (0 = hoy en la zona del operador, 7, 30 o all), tipo, organización, cuenta y una
/// búsqueda libre. Sin totales: el listado solo crece y se recorre con cursor.
/// </summary>
public sealed record ListPlatformAuditRequest : CursorRequest
{
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
    public string? Period { get; init; } = "30";
    public string? Type { get; init; }
    public Guid? TenantId { get; init; }
    public Guid? AccountId { get; init; }
    public string? Search { get; init; }

    public override string ToString() => nameof(ListPlatformAuditRequest);
}

/// <summary>Una fila de la auditoría ya traducida: qué pasó, sobre qué, quién y con qué motivo.</summary>
public sealed record PlatformAuditEventResponse(Guid Id, DateTime OccurredAtUtc, string Type, string Action,
    string Summary, string Target, Guid? TargetTenantId, Guid? TargetAccountId, string Actor, string ActorRole,
    string? Reason, string? IpAddress);

/// <summary>Una organización presente en los eventos filtrados, con su nombre global actual.</summary>
public sealed record PlatformAuditOrganizationResponse(Guid Id, string Name);

/// <summary>Proyección del reader: un SecurityEvent o un ingreso de identity.LoginAudits, con los nombres actuales.</summary>
public sealed record PlatformAuditRow(Guid Id, DateTime OccurredAtUtc, SecurityEventType? EventType,
    LoginAuditMethod? SignInMethod, bool SignInSucceeded, string? FailureCode, AuditActorKind ActorKind,
    PlatformAuditAccount? Actor, PlatformAuditAccount? TargetAccount, Guid? TargetAccountId, Guid? TargetTenantId,
    string? TargetTenantName, string? Reason, string? MaskedIpAddress, string Changes = "{}")
{
    public override string ToString() => nameof(PlatformAuditRow);
}

/// <summary>Una cuenta nombrada en la auditoría: su nombre o, si no tiene, su correo para enmascarar.</summary>
public sealed record PlatformAuditAccount(string? DisplayName, Email? PrimaryEmail, bool IsDeleted, bool IsOperator)
{
    public override string ToString() => nameof(PlatformAuditAccount);
}
