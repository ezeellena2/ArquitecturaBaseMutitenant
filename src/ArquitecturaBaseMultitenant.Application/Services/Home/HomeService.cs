using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Home;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Home;

/// <summary>
/// Arma el inicio en un solo pedido de lectura (ADR 0040). Con permisos de la organización, las tarjetas de sus áreas;
/// si no, «Tus empresas». «Para resolver» junta lo que cada área aporta por <see cref="IPendingItemSource"/>.
/// </summary>
internal sealed class HomeService(OrganizationHomeAreasBuilder areas, MemberHomeCompaniesBuilder companies,
    IEnumerable<IPendingItemSource> sources, IPermissionService permissions, ICurrentUser currentUser,
    TimeProvider timeProvider, ILogger<HomeService> logger) : IHomeService
{
    public Task<Result<HomeResponse>> GetAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync<HomeResponse>(logger, timeProvider, "GetHome", async () =>
        {
            if (currentUser.UserId is not { } userId || userId == Guid.Empty) return AccessErrors.NotMember;
            var granted = await permissions.GetOrganizationPermissionsAsync(userId, cancellationToken);
            var context = new PendingItemContext(userId, granted);
            var organization = granted.Count > 0 ? await areas.SummarizeAsync(context, cancellationToken) : null;
            var cards = organization is null ? await companies.ListAsync(userId, cancellationToken) : [];

            // Cada fuente lee con el mismo contexto de datos: van de a una, nunca en paralelo.
            var pending = new List<PendingItem>();
            foreach (var source in sources)
                pending.AddRange(await source.ListAsync(context, cancellationToken));
            return new HomeResponse(organization, cards, PendingItemOrder.Sort(pending));
        });
}
