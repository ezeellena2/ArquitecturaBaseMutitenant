using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools.Business;

internal sealed class OrganizationInvitationsTool(AssistantAccessPolicy policy, IOrganizationUserService users) : AssistantTool(policy)
{
    public override string Key => "org_invitations";
    public override Access? Access => Domain.Users.Access.Business;
    public override IReadOnlyList<string> Permissions => [Domain.Authorization.Permissions.Users.Read];
    protected override IReadOnlyList<AssistantToolParameter> Parameters => [new("status", "string", true, ["Pending", "Expired"]), Page, PageSize];
    protected override async Task<Result<AssistantToolData>> ExecuteCoreAsync(AssistantToolArguments args, CancellationToken ct)
    {
        var result = await users.ListAsync(new ListOrganizationUsersRequest { State = args.Text("status") == "Pending" ? OrganizationUserState.Invited : OrganizationUserState.Expired, Page = args.Page, PageSize = args.PageSize }, ct);
        if (result.IsFailure) return result.Error;
        var invitations = new List<AssistantInvitationRowResponse>();
        var copies = new List<AssistantSnapshotSubjectCopy>();
        foreach (var row in result.Value.Items.Take(AssistantTechnicalLimits.QuickRows))
        {
            // State selection and invitation hydration are separate reads; a transition requires a new query.
            if (row.InvitationId is not { } id || row.InvitationExpiresAtUtc is not { } expiresAtUtc)
                return AssistantErrors.QuickQueryNotAvailable;
            invitations.Add(new AssistantInvitationRowResponse(id, row.DisplayName, args.Text("status")!, expiresAtUtc));
            if (row.UserId is { } accountId && row.DisplayName is not null)
                copies.Add(new AssistantSnapshotSubjectCopy(accountId, AssistantSnapshotCopyKind.InvitationDisplayName, id));
        }
        return new AssistantQuickToolData(new AssistantInvitationsQuickDataResponse(invitations, result.Value.TotalCount, result.Value.HasNext))
        { SubjectCopies = copies };
    }
}
