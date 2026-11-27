using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantActions;

/// <summary>
/// El lector de roles de siempre, que se niega a leer la lista entera de la organización (revisión de las acciones, bloque
/// de volumen): una fuente que la lee, en vez de buscar con el lector o leer por id lo que nombra, rompe sus escenarios.
/// </summary>
internal sealed class NoWholeRoleListReader(IRoleReader inner) : IRoleReader
{
    /// <summary>Un host de la misma base con este lector en lugar del de siempre.</summary>
    internal static WebApplicationFactory<Program> Host(ApiFactory factory) => factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
    {
        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IRoleReader));
        services.Remove(descriptor);
        services.AddScoped<IRoleReader>(provider =>
            new NoWholeRoleListReader((IRoleReader)ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!)));
    }));

    public Task<IReadOnlyList<RoleRow>> ListAllAsync(CancellationToken cancellationToken) =>
        throw new InvalidOperationException("An action source read every role of the organization.");

    public Task<RoleFilterCountsResponse> CountFiltersAsync(string? search, CancellationToken cancellationToken) =>
        inner.CountFiltersAsync(search, cancellationToken);

    public Task<PagedResult<RoleRow>> ListAsync(ListRolesRequest request, CancellationToken cancellationToken) =>
        inner.ListAsync(request, cancellationToken);

    public Task<RoleRow?> FindByIdAsync(Guid roleId, CancellationToken cancellationToken) => inner.FindByIdAsync(roleId, cancellationToken);

    public Task<IReadOnlyList<RoleRow>> ListByIdsAsync(IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken) =>
        inner.ListByIdsAsync(roleIds, cancellationToken);

    public Task<bool> ExistsByNameAsync(string name, Guid? companyId, Guid? excludedRoleId, CancellationToken cancellationToken) =>
        inner.ExistsByNameAsync(name, companyId, excludedRoleId, cancellationToken);

    public Task<int> CountUsersAsync(Guid roleId, CancellationToken cancellationToken) => inner.CountUsersAsync(roleId, cancellationToken);

    public Task<IReadOnlyList<Guid>> ListOrganizationRoleIdsAsync(Guid userId, CancellationToken cancellationToken) =>
        inner.ListOrganizationRoleIdsAsync(userId, cancellationToken);

    public Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> ListCompanyRoleIdsAsync(Guid userId, CancellationToken cancellationToken) =>
        inner.ListCompanyRoleIdsAsync(userId, cancellationToken);

    public Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> ListPermissionsByRoleAsync(IReadOnlyCollection<Guid> roleIds,
        CancellationToken cancellationToken) => inner.ListPermissionsByRoleAsync(roleIds, cancellationToken);
}
