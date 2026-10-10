using ArquitecturaBaseMultitenant.Application.Common.Logging;
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
    IEnumerable<IPendingItemSource> sources, IPermissionService permissions, ICurrentUser currentUser,
    TimeProvider timeProvider, ILogger<HomeService> logger) : IHomeService
{
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
