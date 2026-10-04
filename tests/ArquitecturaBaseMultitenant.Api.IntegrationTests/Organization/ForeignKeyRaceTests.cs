using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Organization;

/// <summary>
/// Asignar un rol mientras otro pedido lo borra choca con la clave foránea de la base (<c>ForeignKeys</c>). El que
/// guarda segundo recibe la misma respuesta que si hubiera llegado después, nunca un 500.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class ForeignKeyRaceTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Deleting_a_role_that_another_request_just_assigned_answers_that_it_has_people()
    {
        var afterCount = new Hold();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            Decorate<IRoleReader>(services, inner => new HeldRoleReader(inner, afterFind: null, afterCount))));
        var staged = await StageAsync(host.Services, "Rol asignado al borrar", "rol-asignado-al-borrar");

        // El borrado contó cero personas; antes de su commit, otro pedido asigna el rol y guarda.
        afterCount.Arm();
        var deletion = Task.Run(() => DeleteRoleAsync(host.Services, staged), Ct);
        await afterCount.Reached.Task.WaitAsync(TimeSpan.FromSeconds(15), Ct);
        var added = await AddMemberAsync(host.Services, staged);
        afterCount.Release();
        var deleted = await deletion;

        Assert.True(added.IsSuccess, added.IsFailure ? added.Error.Code : null);
        Assert.Equal(RoleErrors.HasUsersCode, deleted.Error.Code);
        Assert.Equal(ErrorType.Conflict, deleted.Error.Type);
        Assert.Equal(1, deleted.Error.Metadata![RoleErrors.UserCountKey]);
    }

    [Fact]
    public async Task Assigning_a_role_that_another_request_just_deleted_answers_that_the_role_does_not_exist()
    {
        var afterFind = new Hold();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            Decorate<IRoleReader>(services, inner => new HeldRoleReader(inner, afterFind, afterCount: null))));
        var staged = await StageAsync(host.Services, "Rol borrado al asignar", "rol-borrado-al-asignar");

        // La asignación leyó el rol; antes de su commit, otro pedido lo borra y guarda.
        afterFind.Arm();
        var adding = Task.Run(() => AddMemberAsync(host.Services, staged), Ct);
        await afterFind.Reached.Task.WaitAsync(TimeSpan.FromSeconds(15), Ct);
        var deleted = await DeleteRoleAsync(host.Services, staged);
        afterFind.Release();
        var added = await adding;

        Assert.True(deleted.IsSuccess, deleted.IsFailure ? deleted.Error.Code : null);
        Assert.Equal(RoleErrors.NotFoundCode, added.Error.Code);
        Assert.Equal(ErrorType.NotFound, added.Error.Type);
    }

    private static async Task<Result> DeleteRoleAsync(IServiceProvider provider, Staged staged)
    {
        await using var scope = provider.CreateAsyncScope();
        // Como un pedido con acceso Empresa a la organización.
        scope.ServiceProvider.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(staged.TenantId,
            TenantKind.Business);
        return await scope.ServiceProvider.GetRequiredService<IRoleService>()
            .DeleteAsync(new DeleteRoleRequest(staged.RoleId, staged.RoleVersion), Ct);
    }

    private static async Task<Result> AddMemberAsync(IServiceProvider provider, Staged staged)
    {
        await using var scope = provider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(staged.TenantId,
            TenantKind.Business);
        return await scope.ServiceProvider.GetRequiredService<ICompanyMembershipService>()
            .AddAsync(new AddCompanyMemberRequest(staged.CompanyId, staged.MemberId, [staged.RoleId]), Ct);
    }

    // Una organización con su Dueña, una segunda persona sin empresa, una empresa y un rol que vale solo en ella.
    private static async Task<Staged> StageAsync(IServiceProvider provider, string name, string slug)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var ownerId = Guid.Empty;
        var personId = Guid.Empty;
        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var users = services.GetRequiredService<IUserRepository>();
            ownerId = (await users.CreateAsync(name, "es-AR", "America/Argentina/Buenos_Aires", ct)).Id;
            personId = (await users.CreateAsync("Paula Méndez", "es-AR", "America/Argentina/Buenos_Aires", ct)).Id;
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);

        var tenant = Tenant.CreateBusiness(name, requiresApproval: false);
        Assert.True(tenant.Activate().IsSuccess);
        Assert.True(tenant.SetSlug(TenantSlug.Create(slug).Value).IsSuccess);
        var company = Company.Create(Guid.CreateVersion7(), "Norte", null, "America/Argentina/Buenos_Aires").Value;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenant.Id);
        await unitOfWork.ExecuteInTransactionAsync(_ =>
        {
            services.GetRequiredService<ICompanyRepository>().Add(company);
            services.GetRequiredService<TenantSpaceProvisioner>().Stage(tenant,
                TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"), [ownerId, personId], ownerId,
                company.Id);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);

        var memberId = Guid.Empty;
        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            memberId = (await services.GetRequiredService<IMemberRepository>().GetByUserIdAsync(personId, ct))!.Id;
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);

        var roles = services.GetRequiredService<IRoleService>();
        var roleId = await roles.CreateAsync(new CreateRoleRequest("Lectura en Norte", null, RoleScope.SpecificCompany,
            company.Id, [Permissions.Company.Members.Read]), Ct);
        Assert.True(roleId.IsSuccess, roleId.IsFailure ? roleId.Error.Code : null);
        var role = await roles.GetAsync(roleId.Value, Ct);
        return new Staged(tenant.Id, company.Id, memberId, roleId.Value, role.Value.Version);
    }

    private static void Decorate<T>(IServiceCollection services, Func<T, T> decorate)
        where T : class
    {
        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(T));
        services.Remove(descriptor);
        services.AddScoped(provider =>
            decorate((T)ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!)));
    }

    private sealed record Staged(Guid TenantId, Guid CompanyId, Guid MemberId, Guid RoleId, uint RoleVersion);

    /// <summary>Retiene al primer pedido que pasa después de armarla, hasta que el otro pedido termina.</summary>
    private sealed class Hold
    {
        private int _armed;

        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private TaskCompletionSource Released { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Arm() => Interlocked.Exchange(ref _armed, 1);

        public void Release() => Released.TrySetResult();

        public async Task PassAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref _armed, 0) == 0) return;
            Reached.TrySetResult();
            await Released.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
        }
    }

    private sealed class HeldRoleReader(IRoleReader inner, Hold? afterFind, Hold? afterCount) : IRoleReader
    {
        public async Task<RoleRow?> FindByIdAsync(Guid roleId, CancellationToken cancellationToken)
        {
            var role = await inner.FindByIdAsync(roleId, cancellationToken);
            if (afterFind is not null) await afterFind.PassAsync(cancellationToken);
            return role;
        }

        public async Task<int> CountUsersAsync(Guid roleId, CancellationToken cancellationToken)
        {
            var count = await inner.CountUsersAsync(roleId, cancellationToken);
            if (afterCount is not null) await afterCount.PassAsync(cancellationToken);
            return count;
        }

        public Task<bool> ExistsByNameAsync(string name, Guid? companyId, Guid? excludedRoleId,
            CancellationToken cancellationToken) =>
            inner.ExistsByNameAsync(name, companyId, excludedRoleId, cancellationToken);

        public Task<IReadOnlyList<RoleRow>> ListAllAsync(CancellationToken cancellationToken) =>
            inner.ListAllAsync(cancellationToken);

        public Task<PagedResult<RoleRow>> ListAsync(ListRolesRequest request, CancellationToken cancellationToken) =>
            inner.ListAsync(request, cancellationToken);

        public Task<IReadOnlyList<Guid>> ListRoleIdsForUserAsync(Guid userId, Guid? companyId,
            CancellationToken cancellationToken) => inner.ListRoleIdsForUserAsync(userId, companyId, cancellationToken);

        public Task<IReadOnlyList<string>> ListPermissionsForRoleAsync(Guid roleId,
            CancellationToken cancellationToken) => inner.ListPermissionsForRoleAsync(roleId, cancellationToken);
    }
}
