using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Home;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Home;

/// <summary>
/// Arma el inicio en un solo pedido de lectura (ADR 0040). Con permisos asignados en la organización, las tarjetas de sus áreas;
/// si no, «Tus empresas» y, en «Para resolver», lo de las empresas donde tiene roles. «Para resolver» junta lo que cada
/// área aporta por <see cref="IPendingItemSource"/>.
/// </summary>
internal sealed class HomeService(OrganizationHomeAreasBuilder areas, MemberHomeCompaniesBuilder companies,
    IEnumerable<IPendingItemSource> sources, IEnumerable<IModuleHomeCardSource> moduleCards, IPermissionService permissions,
    ICurrentUser currentUser, TimeProvider timeProvider, ILogger<HomeService> logger) : IHomeService
{
    public Task<Result<HomeResponse>> GetSummaryAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync<HomeResponse>(logger, timeProvider, "GetHomeSummary", async () =>
        {
            if (currentUser.UserId is not { } userId || userId == Guid.Empty) return AccessErrors.NotMember;
            var assigned = await permissions.GetAssignedOrganizationPermissionsAsync(userId, cancellationToken);
            var granted = await permissions.GetOrganizationPermissionsAsync(userId, cancellationToken);
            var byCompany = assigned.Count > 0
                ? new Dictionary<Guid, IReadOnlyList<string>>()
                : await permissions.GetPermissionsByCompanyAsync(userId, cancellationToken);
            var context = new PendingItemContext(userId, granted, byCompany);
            var organization = assigned.Count > 0 ? await areas.SummarizeAsync(context, cancellationToken) : null;
            var cards = organization is null ? await companies.ListAsync(userId, byCompany, cancellationToken) : [];
            var contributions = new List<(IPendingItemSource Source, IReadOnlyList<PendingGroup> Groups)>();
            foreach (var source in sources)
            {
                if (source is IPendingGroupSource grouped)
                    contributions.Add((source, await grouped.ListGroupsAsync(context, cancellationToken)));
                else
                    contributions.Add((source, Group(await source.ListAsync(context, cancellationToken), preview: false)));
            }

            // Una relectura por ámbito después de todas las fuentes, sin autorizar cada fila por separado.
            var fresh = new PendingItemContext(userId,
                await permissions.GetOrganizationPermissionsAsync(userId, cancellationToken),
                assigned.Count > 0 ? null : await permissions.GetPermissionsByCompanyAsync(userId, cancellationToken));
            var pending = new List<PendingGroup>();
            foreach (var (source, groups) in contributions)
            {
                if (source is IPendingGroupSource grouped)
                    pending.AddRange(await grouped.RevalidateGroupsAsync(groups, fresh, cancellationToken));
                else
                {
                    // Compatibilidad de fuentes previas: conservan su autorización propia hasta adoptar el puerto agrupado.
                    var allowed = new List<PendingItem>();
                    foreach (var item in groups.SelectMany(group => group.Items))
                        if (await source.CanResolveAsync(item, userId, cancellationToken)) allowed.Add(item);
                    pending.AddRange(Group(allowed));
                }
            }
            var ordered = pending.Where(group => group.TotalCount > 0 && group.Items.Count > 0)
                .OrderByDescending(group => group.Items.Any(item => item.Urgent))
                .ThenBy(group => group.Items[0].SinceUtc is null)
                .ThenBy(group => group.Items[0].SinceUtc)
                .ThenBy(group => group.Area, StringComparer.Ordinal)
                .ThenBy(group => group.Kind, StringComparer.Ordinal)
                .ThenBy(group => group.CompanyId).ToArray();
            var modules = new List<HomeModuleCard>();
            if (organization is not null)
                foreach (var source in moduleCards)
                    if (await source.GetAsync(fresh, cancellationToken) is { } card) modules.Add(card);
            return new HomeResponse(organization, cards,
                PendingItemOrder.Sort(ordered.SelectMany(group => group.Items)), ordered,
                modules.Count == 0 ? null : [.. modules.OrderBy(card => card.Key, StringComparer.Ordinal).Take(HomeResponse.ModuleCardLimit)]);
        });

    private static IReadOnlyList<PendingGroup> Group(IReadOnlyList<PendingItem> items, bool preview = true) =>
        [.. items.GroupBy(item => (item.Area, item.Kind, item.CompanyId, item.CompanyName))
            .Select(group => new PendingGroup(group.Key.Area, group.Key.Kind, group.Count(),
                preview ? PendingItemOrder.Sort(group).Take(PendingGroup.PreviewLimit).ToArray() : group.ToArray(),
                group.Key.CompanyId, group.Key.CompanyName))];

    public Task<Result<HomeResponse>> GetAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync<HomeResponse>(logger, timeProvider, "GetHome", async () =>
        {
            if (currentUser.UserId is not { } userId || userId == Guid.Empty) return AccessErrors.NotMember;
            var assigned = await permissions.GetAssignedOrganizationPermissionsAsync(userId, cancellationToken);
            var granted = await permissions.GetOrganizationPermissionsAsync(userId, cancellationToken);
            var byCompany = assigned.Count > 0
                ? new Dictionary<Guid, IReadOnlyList<string>>()
                : await permissions.GetPermissionsByCompanyAsync(userId, cancellationToken);
            var context = new PendingItemContext(userId, granted, byCompany);
            var organization = assigned.Count > 0 ? await areas.SummarizeAsync(context, cancellationToken) : null;
            var cards = organization is null ? await companies.ListAsync(userId, byCompany, cancellationToken) : [];

            // Cada fuente lee con el mismo contexto de datos: van de a una, nunca en paralelo.
            var contributions = new List<(IPendingItemSource Source, PendingItem Item)>();
            foreach (var source in sources)
                contributions.AddRange((await source.ListAsync(context, cancellationToken)).Select(item => (source, item)));
            // El contexto inicial guía la lectura, pero no autoriza entregar datos tras una revocación durante ella.
            var pending = new List<PendingItem>();
            foreach (var (source, item) in contributions)
                if (await source.CanResolveAsync(item, userId, cancellationToken)) pending.Add(item);
            return new HomeResponse(organization, cards, PendingItemOrder.Sort(pending));
        });
}
