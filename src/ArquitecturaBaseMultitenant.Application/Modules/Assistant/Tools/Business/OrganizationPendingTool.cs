using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;
using ArquitecturaBaseMultitenant.Application.Services.OrganizationSettings;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools.Business;

internal sealed class OrganizationPendingTool(AssistantAccessPolicy policy, IHomeService home, IOrganizationUserReader users,
    TimeProvider clock) : AssistantTool(policy)
{
    public override string Key => "org_pending";
    public override Access? Access => Domain.Users.Access.Business;
    public override IReadOnlyList<string> Permissions => [];
    protected override IReadOnlyList<AssistantToolParameter> Parameters => [];
    protected override async Task<Result<AssistantToolData>> ExecuteCoreAsync(AssistantToolArguments args, CancellationToken ct)
    {
        var result = await home.GetAsync(ct);
        if (result.IsFailure) return result.Error;
        var pending = result.Value.Pending.Take(20).ToArray();
        var subjects = new HashSet<Guid>();
        foreach (var item in pending.Where(item => item.Subject is not null))
        {
            if (item.Area == OrganizationDomainPendingItems.Area && item.Kind == OrganizationDomainPendingItems.DomainUnverified)
                continue; // This closed source copies an organization domain, not account data.
            if (item.Area != OrganizationUserPendingItems.Area || item.TargetId is not { } memberId ||
                item.Kind is not (OrganizationUserPendingItems.DeletionRequested or OrganizationUserPendingItems.InvitationExpired))
                return AssistantErrors.QuickQueryNotAvailable;
            var member = await users.FindByIdAsync(memberId, clock.GetUtcNow().UtcDateTime, ct);
            if (member is null) return AssistantErrors.QuickQueryNotAvailable;
            var expected = item.Kind == OrganizationUserPendingItems.DeletionRequested ? member.DisplayName : member.Email?.DisplayValue;
            if (!string.Equals(expected, item.Subject, StringComparison.Ordinal)) return AssistantErrors.QuickQueryNotAvailable;
            // TargetId is MemberId, never an inferred identity. A null UserId is an organization-owned invitation.
            if (member.UserId is { } accountId) subjects.Add(accountId);
        }
        return new AssistantPendingToolData([.. pending.Select(row => new AssistantPendingToolRow(row.Area, row.Kind, row.Subject, row.Urgent))], result.Value.Pending.Count > 20)
        {
            SubjectUserIds = subjects.Order().ToArray(), PrivacySubjectsKnown = true,
        };
    }
}
