using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Services.Platform;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Platform;

[Collection(ApiTestGroup.Name)]
public sealed class PlatformScopeTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(PlatformRole.Owner, true)]
    [InlineData(PlatformRole.Support, false)]
    public async Task Private_access_requires_persisted_permission_and_committed_event(PlatformRole role, bool allowed)
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenant = Tenant.CreateBusiness("Alcance privado", false);
        Assert.True(tenant.Activate().IsSuccess);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var work = services.GetRequiredService<IUnitOfWork>();
        var context = services.GetRequiredService<ApplicationDbContext>();
        await work.ExecuteInTransactionAsync(async ct =>
        {
            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByIdAsync(account.UserId.ToString("D"));
            Assert.NotNull(user);
            user.GrantPlatformOperator();
            Assert.True((await users.UpdateAsync(user)).Succeeded);
            services.GetRequiredService<IPlatformRoleAssignmentRepository>().Add(PlatformRoleAssignment.Create(user.Id, role));
            services.GetRequiredService<ITenantRepository>().Add(tenant);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        using (services.GetRequiredService<ITenantScope>().Enter(tenant.Id))
            await work.ExecuteInTransactionAsync(_ =>
            {
                services.GetRequiredService<IMemberRepository>().Add(Member.Invite());
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        Assert.False(await context.Members.AsNoTracking().AnyAsync(m => m.TenantId == tenant.Id, Ct));

        var access = new PlatformScopeAccessService(new Current(account.UserId),
            services.GetRequiredService<IPlatformPermissionService>(), services.GetRequiredService<ISecurityEventRepository>(),
            work, services.GetRequiredService<TimeProvider>(), services.GetRequiredService<ILogger<PlatformScopeAccessService>>());
        var guard = new PlatformActionGuard(access, services.GetRequiredService<ITenantScope>());
        var result = await guard.EnterAsync(tenant.Id, "Verificar aislamiento", Ct);
        Assert.Equal(allowed, result.IsSuccess);
        Assert.Null(context.Database.CurrentTransaction);
        // Otra conexión confirma que el evento ya está guardado antes de consultar datos privados.
        await using var auditScope = factory.Services.CreateAsyncScope();
        var stored = await auditScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().SecurityEvents.AsNoTracking()
            .CountAsync(e => e.TargetTenantId == tenant.Id && e.Type == SecurityEventType.PlatformTenantEntered, Ct);
        Assert.Equal(allowed ? 1 : 0, stored);
        if (allowed)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                using var lease = result.Value;
                Assert.Equal(account.UserId, lease.OperatorId);
                Assert.True(await context.Members.AsNoTracking().AnyAsync(m => m.TenantId == tenant.Id, Ct));
                throw new InvalidOperationException("Test scope restoration.");
            });
        }
        Assert.False(await context.Members.AsNoTracking().AnyAsync(m => m.TenantId == tenant.Id, Ct));
    }

    private sealed class Current(Guid id) : ICurrentUser
    {
        public Guid? UserId => id;
        public Access? Access => Domain.Users.Access.Platform;
    }
}
