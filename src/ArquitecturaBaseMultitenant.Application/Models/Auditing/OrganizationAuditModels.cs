using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Models.Auditing;

public sealed record ListOrganizationAuditRequest : CursorRequest
{
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
    public string? Period { get; init; } = "30";

    public string? Category { get; init; }

    public Guid? ActorId { get; init; }

    /// <summary>
    /// «Quién» por tipo, con el <c>actorKind</c> de la lista de «Quién»: <c>System</c> («Sistema»),
    /// <c>PlatformOperator</c> («Plataforma», con sus acciones sobre la organización) o <c>User</c> (las personas).
    /// </summary>
    public string? ActorKind { get; init; }

    public string? Search { get; init; }
}

public sealed record OrganizationAuditRow(Guid Id, DateTime OccurredAtUtc, AuditAction Action, string EntityType,
    Guid EntityId, Guid? ActorId, AuditActorKind ActorKind, string? ActorDisplayName,
    string? AffectedDisplayName, string? CompanyName, string Changes, Email? ActorEmail = null);

public sealed record OrganizationAuditChangeResponse(string Field, string? OldValue, string? NewValue);

public sealed record OrganizationAuditActorRow(Guid? ActorId, AuditActorKind ActorKind, string? DisplayName);

public sealed record OrganizationAuditActorResponse(Guid? ActorId, string ActorKind, string ActorText,
    string? DisplayName);

public sealed record OrganizationAuditEntryResponse(Guid Id, DateTime OccurredAtUtc, string Action,
    string ActionText, string EntityType, string EntityText, string? AffectedDisplayName, string WhereText,
    string ActorKind, string ActorText, Guid? ActorId, string? ActorDisplayName,
    IReadOnlyList<OrganizationAuditChangeResponse> Changes, Email? ActorEmail);
