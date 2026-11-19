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
            var (context, byCompany, assigned) = await ContextAsync(userId, cancellationToken);
            var organization = assigned ? await areas.SummarizeAsync(context, cancellationToken) : null;
            var cards = organization is null ? await companies.ListAsync(userId, byCompany, cancellationToken) : [];
            var contributions = new List<(IPendingItemSource Source, IReadOnlyList<PendingGroup> Groups)>();
            foreach (var source in sources)
            {
                if (source is IPendingGroupSource grouped)
                    contributions.Add((source, await grouped.ListGroupsAsync(context, cancellationToken)));
                else
                    contributions.Add((source, Group(await source.ListAsync(context, cancellationToken), preview: false)));
            }

            // Una relectura por ámbito después de todas las fuentes, sin autorizar cada fila por separado: otra lectura de
            // cada proyección, juntas (base de la 12).
            var current = await permissions.GetBusinessPermissionsAsync(userId, cancellationToken);
            var fresh = new PendingItemContext(userId, current.Organization, assigned ? null : current.ByCompany);
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
            // La lista plana anterior es el adaptador vacío que publica el contrato (LegacyPendingLimit = 0):
            // las filas viajan sólo en los grupos acotados.
            return new HomeResponse(organization, cards, [], ordered,
                modules.Count == 0 ? null : [.. modules.OrderBy(card => card.Key, StringComparer.Ordinal).Take(HomeResponse.ModuleCardLimit)]);
        });

    // Inicio-Org con permisos asignados en la organización; si no, Inicio-Miembro con los de cada empresa. Las tres vistas
    // salen de una lectura de cada proyección (base de la 12, permisos.md).
    private async Task<(PendingItemContext Context, IReadOnlyDictionary<Guid, IReadOnlyList<string>> ByCompany,
        bool Assigned)> ContextAsync(Guid userId, CancellationToken cancellationToken)
    {
        var granted = await permissions.GetBusinessPermissionsAsync(userId, cancellationToken);
        var assigned = granted.Assigned.Count > 0;
        var byCompany = assigned ? new Dictionary<Guid, IReadOnlyList<string>>() : granted.ByCompany;
        return (new PendingItemContext(userId, granted.Organization, byCompany), byCompany, assigned);
    }

    private static IReadOnlyList<PendingGroup> Group(IReadOnlyList<PendingItem> items, bool preview = true) =>
        [.. items.GroupBy(item => (item.Area, item.Kind, item.CompanyId, item.CompanyName))
            .Select(group => new PendingGroup(group.Key.Area, group.Key.Kind, group.Count(),
                preview ? PendingItemOrder.Sort(group).Take(PendingGroup.PreviewLimit).ToArray() : group.ToArray(),
                group.Key.CompanyId, group.Key.CompanyName))];

    public Task<Result<HomeResponse>> GetAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync<HomeResponse>(logger, timeProvider, "GetHome", async () =>
        {
            if (currentUser.UserId is not { } userId || userId == Guid.Empty) return AccessErrors.NotMember;
            var (context, byCompany, assigned) = await ContextAsync(userId, cancellationToken);
            var organization = assigned ? await areas.SummarizeAsync(context, cancellationToken) : null;
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
