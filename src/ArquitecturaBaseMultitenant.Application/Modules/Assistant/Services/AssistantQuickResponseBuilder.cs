using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Resources;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantQuickResponseBuilder(DisplayFormatter formatter, IProfileService profiles)
{
    internal async Task<AssistantQuickCompositionResponse> BuildAsync(AssistantQuickDefinition query, AssistantQuickDataResponse data,
        string culture, string zone, CancellationToken ct, IReadOnlyList<AssistantSnapshotSubjectCopy>? subjectCopies = null)
    {
        var context = await formatter.CreateAsync(culture, zone, ct);
        var title = AssistantToolTexts.Get(query.LabelKey, context.Profile);
        var (count, more) = Summary(data);
        var text = data is AssistantInteractionsQuickDataResponse { Interactions.TotalCount: 0 }
            ? AssistantToolTexts.Get("Quick.InteractionsEmpty", context.Profile)
            : DisplayFormatter.FormatResource(AssistantToolTexts.Get("Quick.Summary", context.Profile), context.Profile,
                title, formatter.FormatDecimal(count, 0, context));
        if (count == 0 && data is not AssistantInteractionsQuickDataResponse)
            text += " " + AssistantToolTexts.Get("Quick.Empty", context.Profile);
        if (more) text += " " + AssistantToolTexts.Get("Quick.More", context.Profile);
        var links = new List<AssistantLinkResponse>();
        if (query.Key == "my_organizations" && data is AssistantOrganizationsQuickDataResponse organizations)
        {
            var profile = await profiles.GetAsync(ct);
            if (profile.IsSuccess)
            {
                var authorized = profile.Value.Organizations.Where(row => row.IsSelectable).Select(row => row.Id).ToHashSet();
                foreach (var row in organizations.Organizations.Where(row => authorized.Contains(row.Id)).Take(20))
                    links.Add(Link(AssistantRouteKey.PersonalOrganizations, context.Profile, tenantId: row.Id, label: "Link.ViewOrganization"));
            }
        }
        else if (query.Key != "my_messages")
        {
            var route = query.Key switch
            {
                "active_users" or "expired_invitations" or "owners" => AssistantRouteKey.BusinessUsers,
                "recent_changes" => AssistantRouteKey.BusinessAudit,
                "pending_organizations" => AssistantRouteKey.PlatformOrganizations,
                "pending_recoveries" => AssistantRouteKey.PlatformRecoveries,
                "today_security" => AssistantRouteKey.PlatformAudit,
                "assistant_usage" => AssistantRouteKey.PlatformAssistantUsage,
                "my_signin" => AssistantRouteKey.PersonalAccount,
                _ => throw new InvalidOperationException("A quick query has no authorized route."),
            };
            var status = query.Key switch
            {
                "active_users" => AssistantLinkStatus.Active,
                "expired_invitations" => AssistantLinkStatus.Expired,
                "pending_organizations" or "pending_recoveries" => AssistantLinkStatus.Pending,
                _ => (AssistantLinkStatus?)null,
            };
            links.Add(Link(route, context.Profile, status: status, label: query.Key == "my_signin" ? "Link.ViewAccount" : "Link.View"));
        }
        return new AssistantQuickCompositionResponse(title, new AssistantQuickResultResponse(text, links, data)
        { SubjectCopies = subjectCopies ?? [] }) { SubjectCopies = subjectCopies ?? [] };
    }

    private static AssistantLinkResponse Link(AssistantRouteKey route, CultureProfile profile, Guid? tenantId = null, AssistantLinkStatus? status = null, string label = "Link.View") =>
        new(route, new AssistantLinkParametersResponse(null, null, null, tenantId, null),
            new AssistantLinkQueryResponse(status, null, null, null), AssistantToolTexts.Get(label, profile));

    private static (int Count, bool More) Summary(AssistantQuickDataResponse data) => data switch
    {
        AssistantPeopleQuickDataResponse value => (value.TotalCount, value.HasMore),
        AssistantInvitationsQuickDataResponse value => (value.TotalCount, value.HasMore),
        AssistantOrganizationsQuickDataResponse value => (value.TotalCount, value.HasMore),
        AssistantRecoveriesQuickDataResponse value => (value.TotalCount, value.HasMore),
        AssistantAuditQuickDataResponse value => (value.Changes.Count, value.HasMore),
        AssistantSecurityQuickDataResponse value => (value.Events.Count, value.HasMore),
        AssistantUsageQuickDataResponse value => (value.Usage.TotalQueries, value.Usage.Rows.HasNext),
        AssistantAccountQuickDataResponse value => (value.LoginMethods.Count, false),
        AssistantInteractionsQuickDataResponse value => (value.Interactions.TotalCount, value.Interactions.HasNext),
        _ => throw new InvalidOperationException("The quick data variant is unsupported."),
    };
}
