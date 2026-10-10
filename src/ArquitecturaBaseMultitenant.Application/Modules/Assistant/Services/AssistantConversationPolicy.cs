using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantConversationPolicy(AssistantNoticePolicy notices, AssistantToolResolver tools,
    IProfileService profiles, ICurrentUser user, AssistantAccessPolicy access)
{
    internal Task<Result> AuthorizeAsync(CancellationToken ct) => notices.CheckAcceptedAsync(notices.CurrentVersion, ct);

    internal async Task<Result> AuthorizeQuickAsync(string key, IReadOnlyList<AssistantLinkResponse> links, CancellationToken ct)
    {
        var allowed = await AuthorizeAsync(ct);
        if (allowed.IsFailure) return allowed;
        var query = AssistantQuickCatalog.All.SingleOrDefault(row => row.Key == key && row.Access == user.Access);
        if (query is null || !await tools.IsAvailableAsync(query.ToolKey, ct)) return AssistantErrors.QuickQueryNotAvailable;
        return await AuthorizeLinksAsync(links, ct);
    }

    internal async Task<Result> AuthorizeLinksAsync(IReadOnlyList<AssistantLinkResponse> links, CancellationToken ct)
    {
        foreach (var link in links)
        {
            var rule = link.RouteKey switch
            {
                AssistantRouteKey.BusinessUsers => (Access.Business, Permissions.Users.Read),
                AssistantRouteKey.BusinessRoles => (Access.Business, Permissions.Roles.Read),
                AssistantRouteKey.BusinessCompanies => (Access.Business, Permissions.Companies.Read),
                AssistantRouteKey.BusinessAudit => (Access.Business, Permissions.Audit.Read),
                AssistantRouteKey.BusinessHome => (Access.Business, (string?)null),
                AssistantRouteKey.PlatformOrganizations => (Access.Platform, PlatformPermissions.Tenants.Read),
                AssistantRouteKey.PlatformAccounts => (Access.Platform, PlatformPermissions.Accounts.Read),
                AssistantRouteKey.PlatformRecoveries => (Access.Platform, PlatformPermissions.Recoveries.Manage),
                AssistantRouteKey.PlatformAudit => (Access.Platform, PlatformPermissions.Audit.Read),
                AssistantRouteKey.PlatformAssistantUsage => (Access.Platform, PlatformPermissions.Assistant.Read),
                AssistantRouteKey.PersonalAccount or AssistantRouteKey.PersonalOrganizations => (Access.Consumer, (string?)null),
                _ => ((Access)(-1), (string?)null),
            };
            if (!Enum.IsDefined(rule.Item1) || !await access.CanExecuteAsync(rule.Item1, rule.Item2 is { } permission ? [permission] : [], link.Parameters.CompanyId, ct))
                return AssistantErrors.QuickQueryNotAvailable;
            // Only PersonalOrganizations accepts an organization parameter. No link may impose an access context.
            if (link.Parameters.TenantId is not null && link.RouteKey != AssistantRouteKey.PersonalOrganizations)
                return AssistantErrors.QuickQueryNotAvailable;
        }
        if (links.Any(link => link.RouteKey == AssistantRouteKey.PersonalOrganizations))
        {
            var profile = await profiles.GetAsync(ct);
            if (profile.IsFailure) return profile.Error;
            var own = profile.Value.Organizations.Where(row => row.IsSelectable).Select(row => row.Id).ToHashSet();
            if (links.Any(link => link.RouteKey == AssistantRouteKey.PersonalOrganizations &&
                (link.Parameters.TenantId is not { } id || !own.Contains(id)))) return AssistantErrors.QuickQueryNotAvailable;
        }
        return Result.Success();
    }
}
