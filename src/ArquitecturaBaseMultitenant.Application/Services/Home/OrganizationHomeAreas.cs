using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Auditing;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Application.Models.Home;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.Home;

/// <summary>
/// Las tarjetas de Inicio-Org. Cada una cuenta con el reader de su área, así el número es el mismo que muestra el área,
/// y solo se calcula si quien mira tiene el permiso de verla.
/// </summary>
internal sealed class OrganizationHomeAreas(IOrganizationUserReader users, IRoleReader roles, ICompanyReader companies,
    IPublicPageReader publicPages, ITenantDomainReader domains, IOrganizationAuditReader audit,
    TimeProvider timeProvider)
{
    public async Task<HomeOrganizationSummary> SummarizeAsync(PendingItemContext context,
        CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        return new HomeOrganizationSummary(
            context.Can(Permissions.Users.Read)
                ? new HomeCount((await users.GetFilterCountsAsync(new ListOrganizationUsersRequest(), nowUtc,
                    cancellationToken)).All)
                : null,
            context.Can(Permissions.Roles.Read)
                ? new HomeCount((await roles.ListAsync(new ListRolesRequest(), cancellationToken)).TotalCount)
                : null,
            context.Can(Permissions.Companies.Read)
                ? new HomeCount((await companies.GetFilterCountsAsync(new ListCompaniesRequest(), cancellationToken)).All)
                : null,
            context.Can(Permissions.PublicPage.Manage) ? await PublicPageAsync(cancellationToken) : null,
            context.Can(Permissions.Settings.Read) ? await SettingsAsync(cancellationToken) : null,
            context.Can(Permissions.Audit.Read) ? await AuditAsync(cancellationToken) : null);
    }

    private async Task<HomePublicPage> PublicPageAsync(CancellationToken cancellationToken)
    {
        var page = await publicPages.FindCurrentAsync(cancellationToken);
        return new HomePublicPage(page switch
        {
            { PublishBlockedAtUtc: not null } => HomePublicPageState.Blocked,
            { IsPublished: true } => HomePublicPageState.Published,
            _ => HomePublicPageState.Draft,
        });
    }

    // El verificado primero; si todavía no hay, el que espera verificación.
    private async Task<HomeSettings> SettingsAsync(CancellationToken cancellationToken)
    {
        var current = await domains.ListCurrentAsync(cancellationToken);
        return new HomeSettings(current.OrderBy(domain => domain.Status != TenantDomainStatus.Verified)
            .Select(domain => domain.Domain).FirstOrDefault());
    }

    private async Task<HomeAudit> AuditAsync(CancellationToken cancellationToken)
    {
        var last = await audit.ListAsync(new ListOrganizationAuditRequest { Period = "all", Limit = 1 }, null, null,
            cancellationToken);
        return new HomeAudit(last.Items.Count == 0 ? null : last.Items[0].OccurredAtUtc);
    }
}
