using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Domain.Auditing;

namespace ArquitecturaBaseMultitenant.Application.Models.Auditing;

public sealed record ListOrganizationAuditRequest : CursorRequest
{
    public string? Period { get; init; } = "30";

    public string? Category { get; init; }

    public Guid? ActorId { get; init; }

    public string? Search { get; init; }
}

public sealed record OrganizationAuditRow(Guid Id, DateTime OccurredAtUtc, AuditAction Action, string EntityType,
    Guid EntityId, Guid? ActorId, AuditActorKind ActorKind, string? ActorDisplayName,
    string? AffectedDisplayName, string? CompanyName, string Changes, string? ActorEmail = null);

public sealed record OrganizationAuditChangeResponse(string Field, string? OldValue, string? NewValue);

public sealed record OrganizationAuditActorRow(Guid? ActorId, AuditActorKind ActorKind, string? DisplayName);

public sealed record OrganizationAuditActorResponse(Guid? ActorId, string ActorKind, string ActorText,
    string? DisplayName);

public sealed record OrganizationAuditEntryResponse(Guid Id, DateTime OccurredAtUtc, string Action,
    string ActionText, string EntityType, string EntityText, string? AffectedDisplayName, string WhereText,
    string ActorKind, string ActorText, Guid? ActorId, string? ActorDisplayName,
    IReadOnlyList<OrganizationAuditChangeResponse> Changes, string? ActorEmail);
