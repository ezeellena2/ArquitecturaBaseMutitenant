using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

/// <summary>
/// Comprueba que el reader liste únicamente las membresías de la cuenta consultada. Permite elegir acceso
/// sin abrir lecturas privadas de otros espacios.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class UserTenantAccessReaderTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Reader_lists_only_account_memberships_across_tenant_scopes()
    {
        using var client = factory.CreateClient();
        Guid userId;
        Guid otherId;
        var alpha = Tenant.CreateBusiness("Alfa", requiresApproval: false);
        var beta = Tenant.CreateBusiness("Beta", requiresApproval: false);
        var personal = Tenant.CreatePersonal("Personal");
        Assert.True(alpha.Activate().IsSuccess);
        Assert.True(beta.Activate().IsSuccess);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            userId = Guid.Empty;
            otherId = Guid.Empty;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var users = services.GetRequiredService<IUserRepository>();
                userId = (await users.CreateAsync(null, "es-AR", "America/Argentina/Buenos_Aires", ct)).Id;
                otherId = (await users.CreateAsync(null, "es-AR", "America/Argentina/Buenos_Aires", ct)).Id;
                var tenants = services.GetRequiredService<ITenantRepository>();
                tenants.Add(alpha);
                tenants.Add(beta);
                tenants.Add(personal);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }

        var older = new DateTime(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);
        var newer = older.AddDays(1);
        await AddMemberAsync(alpha.Id, userId, older);
        await AddMemberAsync(beta.Id, userId, newer);
        await AddMemberAsync(beta.Id, otherId, newer);
        await AddMemberAsync(personal.Id, userId, newer);

        await using var readScope = factory.Services.CreateAsyncScope();
        var accesses = await readScope.ServiceProvider.GetRequiredService<IUserTenantAccessReader>()
            .ListForUserAsync(userId, Ct);

        Assert.Equal(3, accesses.Count);
        Assert.Contains(accesses, row => row.TenantId == alpha.Id && row.Kind == TenantKind.Business &&
            row.Name == "Alfa" && row.JoinedAtUtc == older && row.MemberStatus == MemberStatus.Active);
        Assert.Contains(accesses, row => row.TenantId == beta.Id && row.Kind == TenantKind.Business &&
            row.Name == "Beta" && row.JoinedAtUtc == newer);
        Assert.Contains(accesses, row => row.TenantId == personal.Id && row.Kind == TenantKind.Personal);
    }

    [Fact]
    public async Task Tenant_admin_reader_counts_only_active_accounts_memberships_and_organizations()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var work = services.GetRequiredService<IUnitOfWork>();
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var userIds = new Guid[5];
        await work.ExecuteInTransactionAsync(async ct =>
        {
            var users = services.GetRequiredService<IUserRepository>();
            for (var index = 0; index < userIds.Length; index++)
                userIds[index] = (await users.CreateAsync(null, "es-AR", "America/Argentina/Buenos_Aires", ct)).Id;

            var pending = await context.Users.SingleAsync(user => user.Id == userIds[1], ct);
            Assert.True(pending.RequestDeletion("Baja de prueba", nowUtc, 30).IsSuccess);
            var suspended = await context.Users.SingleAsync(user => user.Id == userIds[2], ct);
            Assert.True(suspended.Suspend().IsSuccess);
            var deleted = await context.Users.SingleAsync(user => user.Id == userIds[3], ct);
            Assert.True(deleted.RequestDeletion("Baja de prueba", nowUtc, 1).IsSuccess);
            Assert.True(deleted.CompleteDeletion(nowUtc.AddDays(2), "Cuenta eliminada").IsSuccess);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);

        var business = Tenant.CreateBusiness("Organización para conteo de Dueños", requiresApproval: false);
        var pendingBusiness = Tenant.CreateBusiness("Organización pendiente", requiresApproval: true);
        Assert.True(business.Activate().IsSuccess);
        using (services.GetRequiredService<ITenantScope>().Enter(business.Id))
        {
            await work.ExecuteInTransactionAsync(ct =>
            {
                services.GetRequiredService<TenantSpaceProvisioner>().Stage(business,
                    TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"), [userIds[0]]);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
            await work.ExecuteInTransactionAsync(async ct =>
            {
                var tenantAdmin = await context.Roles.SingleAsync(role => role.Name == SystemRoles.TenantAdmin, ct);
                for (var index = 1; index < userIds.Length; index++)
                {
                    var member = Member.Invite(userIds[index]);
                    Assert.True(member.Activate(nowUtc).IsSuccess);
                    if (index == 4) Assert.True(member.Deactivate().IsSuccess);
                    context.Members.Add(member);
                    context.RoleAssignments.Add(RoleAssignment.Assign(member.Id, tenantAdmin, null).Value);
                }
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }

        using (services.GetRequiredService<ITenantScope>().Enter(pendingBusiness.Id))
        {
            await work.ExecuteInTransactionAsync(ct =>
            {
                services.GetRequiredService<TenantSpaceProvisioner>().Stage(pendingBusiness,
                    TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"), [userIds[0]]);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }

        var admins = services.GetRequiredService<ITenantAdminReader>();
        Assert.Equal(1, await admins.CountActiveOwnersAsync(business.Id, Ct));
        Assert.Equal(new[] { business.Id }, await admins.ListActiveTenantIdsForOwnerAsync(userIds[0], Ct));
        Assert.Equal(new[] { business.Id, pendingBusiness.Id }.Order(),
            await admins.ListOwnedBusinessTenantIdsAsync(userIds[0], Ct));

        await work.ExecuteInTransactionAsync(async ct =>
        {
            // Los deltas de membresía confirmados cambiaron el xmin de esta entidad de la fixture.
            await context.Entry(business).ReloadAsync(ct);
            Assert.True(business.Suspend().IsSuccess);
            Assert.True(business.Close().IsSuccess);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);

        Assert.Equal(0, await admins.CountActiveOwnersAsync(business.Id, Ct));
        Assert.Empty(await admins.ListActiveTenantIdsForOwnerAsync(userIds[0], Ct));
    }

    private async Task AddMemberAsync(Guid tenantId, Guid userId, DateTime joinedAtUtc)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var active = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
        {
            var member = Member.Invite(userId);
            Assert.True(member.Activate(joinedAtUtc).IsSuccess);
            services.GetRequiredService<IMemberRepository>().Add(member);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
    }
}
