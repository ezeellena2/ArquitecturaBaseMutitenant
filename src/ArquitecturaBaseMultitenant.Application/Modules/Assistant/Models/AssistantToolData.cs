using System.Text.Json.Serialization;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

// Internal provider projections. These do not extend the frozen HTTP Quick variants.
public abstract record AssistantToolData;
public sealed record AssistantQuickToolData(AssistantQuickDataResponse Data) : AssistantToolData
{
    [JsonIgnore]
    public IReadOnlyList<AssistantSnapshotSubjectCopy> SubjectCopies { get; init; } = [];
}
public sealed record AssistantRolesToolData(IReadOnlyList<AssistantRoleToolRow> Roles, int TotalCount, bool HasMore) : AssistantToolData;
public sealed record AssistantRoleToolRow(Guid Id, string Name, string Scope, Guid? CompanyId, IReadOnlyList<string> Permissions);
public sealed record AssistantCompaniesToolData(IReadOnlyList<AssistantCompanyToolRow> Companies, int TotalCount, bool HasMore) : AssistantToolData;
public sealed record AssistantCompanyToolRow(Guid Id, string Name, string Status, int MemberCount);
public sealed record AssistantPendingToolData(IReadOnlyList<AssistantPendingToolRow> Pending, bool HasMore) : AssistantToolData;
public sealed record AssistantPendingToolRow(string Area, string Kind, string? Subject, bool Urgent);
public sealed record AssistantHelpToolData(IReadOnlyList<AssistantHelpArticleResponse> Articles) : AssistantToolData;
