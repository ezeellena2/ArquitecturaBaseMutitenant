using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.PublicSite;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.PublicSite;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Organization;

/// <summary>
/// Dos pedidos que pasan juntos el control previo chocan con un índice único de la base (<c>UniqueKeys</c>). El que
/// llega segundo recibe la misma respuesta que si hubiera llegado después, nunca un 500.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class UniqueKeyRaceTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Two_simultaneous_roles_with_the_same_name_create_one_and_answer_the_other_with_a_conflict()
    {
        var gate = new BothArrived();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            Decorate<IRoleReader>(services, inner => new GatedRoleReader(inner, gate))));
        var tenantId = await StageOrganizationAsync(host.Services, "Roles simultáneos", "roles-simultaneos");

        // B10: los dos pasan «¿ya existe ese nombre?» antes de que el otro guarde; la base deja entrar a uno solo.
        var results = await Task.WhenAll(CreateRoleAsync(host.Services, tenantId),
            CreateRoleAsync(host.Services, tenantId));

        Assert.True(gate.Both.Task.IsCompletedSuccessfully);
        Assert.Single(results, result => result.IsSuccess);
        var refused = Assert.Single(results, result => result.IsFailure);
        Assert.Equal(RoleErrors.AlreadyExistsCode, refused.Error.Code);
        Assert.Equal(ErrorType.Conflict, refused.Error.Type);
    }

    [Fact]
    public async Task Two_first_saves_of_the_public_page_at_once_keep_one_and_answer_the_other_as_a_concurrent_edit()
    {
        var gate = new BothArrived();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            Decorate<IPublicPageRepository>(services, inner => new GatedPublicPageRepository(inner, gate))));
        var tenantId = await StageOrganizationAsync(host.Services, "Página simultánea", "pagina-simultanea");

        // Los dos ven «sin página guardada» con la versión 0: el segundo choca con «una página por organización».
        var outcomes = await Task.WhenAll(SavePageAsync(host.Services, tenantId),
            SavePageAsync(host.Services, tenantId));

        Assert.True(gate.Both.Task.IsCompletedSuccessfully);
        Assert.Single(outcomes, outcome => outcome is null);
        Assert.Single(outcomes, outcome => outcome is ConcurrencyConflictException);
    }

    private static async Task<Result<Guid>> CreateRoleAsync(IServiceProvider provider, Guid tenantId)
    {
        await using var scope = provider.CreateAsyncScope();
        using var tenantScope = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
        return await scope.ServiceProvider.GetRequiredService<IRoleService>().CreateAsync(
            new CreateRoleRequest("Ventas", null, RoleScope.Organization, null, [Permissions.Users.Read]), Ct);
    }

    private static async Task<Exception?> SavePageAsync(IServiceProvider provider, Guid tenantId)
    {
        await using var scope = provider.CreateAsyncScope();
        // Como un pedido con acceso Empresa: el dato público se sella con la organización activa.
        scope.ServiceProvider.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenantId, TenantKind.Business);
        try
        {
            var result = await scope.ServiceProvider.GetRequiredService<IPublicPageService>().UpdateAsync(
                new UpdatePublicPageRequest("pagina-simultanea", "Página simultánea", null, null, null, null, null,
                    null, null, null, RemoveLogo: false, Version: 0), Ct);
            Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
            return null;
        }
        catch (ConcurrencyConflictException exception)
        {
            return exception;
        }
    }

    private static async Task<Guid> StageOrganizationAsync(IServiceProvider provider, string name, string slug)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var ownerId = Guid.Empty;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            ownerId = (await services.GetRequiredService<IUserRepository>().CreateAsync(name, "es-AR",
                "America/Argentina/Buenos_Aires", ct)).Id;
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);

        var tenant = Tenant.CreateBusiness(name, requiresApproval: false);
        Assert.True(tenant.Activate().IsSuccess);
        Assert.True(tenant.SetSlug(TenantSlug.Create(slug).Value).IsSuccess);
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenant.Id);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            services.GetRequiredService<TenantSpaceProvisioner>().Stage(tenant,
                TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"), [ownerId], ownerId);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        return tenant.Id;
    }

    private static void Decorate<T>(IServiceCollection services, Func<T, T> decorate)
        where T : class
    {
        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(T));
        services.Remove(descriptor);
        services.AddScoped(provider =>
            decorate((T)ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!)));
    }

    /// <summary>Retiene a cada pedido hasta que llegan los dos: así los dos hicieron el control previo.</summary>
    private sealed class BothArrived
    {
        private int _arrivals;

        public TaskCompletionSource Both { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task ArriveAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _arrivals) == 2) Both.TrySetResult();
            await Both.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
        }
    }

    private sealed class GatedRoleReader(IRoleReader inner, BothArrived gate) : IRoleReader
    {
        public Task<RoleFilterCountsResponse> GetFilterCountsAsync(string? search, CancellationToken cancellationToken) =>
            inner.GetFilterCountsAsync(search, cancellationToken);

        public async Task<bool> ExistsByNameAsync(string name, Guid? companyId, Guid? excludedRoleId,
            CancellationToken cancellationToken)
        {
            var exists = await inner.ExistsByNameAsync(name, companyId, excludedRoleId, cancellationToken);
            await gate.ArriveAsync(cancellationToken);
            return exists;
        }

        public Task<IReadOnlyList<RoleRow>> ListAllAsync(CancellationToken cancellationToken) =>
            inner.ListAllAsync(cancellationToken);

        public Task<PagedResult<RoleRow>> ListAsync(ListRolesRequest request, CancellationToken cancellationToken) =>
            inner.ListAsync(request, cancellationToken);

        public Task<RoleRow?> FindByIdAsync(Guid roleId, CancellationToken cancellationToken) =>
            inner.FindByIdAsync(roleId, cancellationToken);

        public Task<int> CountUsersAsync(Guid roleId, CancellationToken cancellationToken) =>
            inner.CountUsersAsync(roleId, cancellationToken);

        public Task<IReadOnlyList<Guid>> ListRoleIdsForUserAsync(Guid userId, Guid? companyId,
            CancellationToken cancellationToken) => inner.ListRoleIdsForUserAsync(userId, companyId, cancellationToken);

        public Task<IReadOnlyList<string>> ListPermissionsForRoleAsync(Guid roleId,
            CancellationToken cancellationToken) => inner.ListPermissionsForRoleAsync(roleId, cancellationToken);
    }

    private sealed class GatedPublicPageRepository(IPublicPageRepository inner, BothArrived gate)
        : IPublicPageRepository
    {
        public async Task<PublicPage?> GetCurrentAsync(uint expectedVersion, CancellationToken cancellationToken)
        {
            var page = await inner.GetCurrentAsync(expectedVersion, cancellationToken);
            await gate.ArriveAsync(cancellationToken);
            return page;
        }

        public void Add(PublicPage page) => inner.Add(page);

        public void MarkUpdated(PublicPage page) => inner.MarkUpdated(page);
    }
}
