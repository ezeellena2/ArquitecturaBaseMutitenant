using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Application.Services.Platform;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Platform;

[Collection(ApiTestGroup.Name)]
public sealed class PlatformOrganizationNoticeTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rejection_and_moderation_reach_only_active_owners_safe_contacts_without_internal_reason(bool moderation)
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var work = services.GetRequiredService<IUnitOfWork>();
        var clock = services.GetRequiredService<TimeProvider>();
        var owner = ApplicationUser.Create(null, "es-AR", "America/Argentina/Buenos_Aires").Value;
        var secondOwner = ApplicationUser.Create(null, "es-AR", "America/Argentina/Buenos_Aires").Value;
        var retired = ApplicationUser.Create(null, "es-AR", "America/Argentina/Buenos_Aires").Value;
        var suspended = ApplicationUser.Create(null, "es-AR", "America/Argentina/Buenos_Aires").Value;
        Assert.True(suspended.Suspend().IsSuccess);
        var other = Tenant.CreateBusiness("Otra " + Guid.NewGuid().ToString("N"), false);
        other.Activate();
        var tenant = Tenant.CreateBusiness("Avisos " + Guid.NewGuid().ToString("N"), false);
        tenant.RecordRegistration(retired.Id, TenantRegistrationSource.SelfService, clock.GetUtcNow().UtcDateTime);
        tenant.Activate();
        tenant.Suspend();
        tenant.Close();
        var foreign = Method(owner.Id, true, clock);
        foreign.AssignManagedByOrganization(other.Id);
        var fallback = Method(owner.Id, false, clock);
        var primary = Method(secondOwner.Id, true, clock);
        var secondary = Method(secondOwner.Id, false, clock);
        var retiredPrimary = Method(retired.Id, true, clock);
        var suspendedPrimary = Method(suspended.Id, true, clock);

        await work.ExecuteInTransactionAsync(_ =>
        {
            db.Users.AddRange(owner, secondOwner, retired, suspended);
            db.LoginMethods.AddRange(foreign, fallback, primary, secondary, retiredPrimary, suspendedPrimary);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        using (services.GetRequiredService<ITenantScope>().Enter(other.Id))
            await work.ExecuteInTransactionAsync(_ =>
            {
                services.GetRequiredService<TenantSpaceProvisioner>().Stage(other, Settings(), [owner.Id], owner.Id);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        using (services.GetRequiredService<ITenantScope>().Enter(tenant.Id))
        {
            await work.ExecuteInTransactionAsync(_ =>
            {
                services.GetRequiredService<TenantSpaceProvisioner>().Stage(tenant, Settings(), [owner.Id, secondOwner.Id, retired.Id, suspended.Id], owner.Id);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
            await work.ExecuteInTransactionAsync(async ct =>
            {
                var members = services.GetRequiredService<IMemberRepository>();
                var second = await members.GetByUserIdAsync(secondOwner.Id, ct);
                var former = await members.GetByUserIdAsync(retired.Id, ct);
                var role = await services.GetRequiredService<IRoleRepository>().GetByNameAsync(SystemRoles.TenantAdmin, ct);
                Assert.NotNull(second);
                Assert.NotNull(former);
                Assert.NotNull(role);
                services.GetRequiredService<IRoleAssignmentRepository>().Add(RoleAssignment.Assign(second.Id, role, null).Value);
                var inactive = await members.GetByUserIdAsync(suspended.Id, ct);
                Assert.NotNull(inactive);
                services.GetRequiredService<IRoleAssignmentRepository>().Add(RoleAssignment.Assign(inactive.Id, role, null).Value);
                Assert.True(former.Remove().IsSuccess);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        await work.ExecuteInTransactionAsync(async ct =>
        {
            var notifier = services.GetRequiredService<PlatformTenantNotifier>();
            if (moderation) await notifier.EnqueuePageModerationAsync(tenant, true, ct);
            else await notifier.EnqueueRejectedAsync(tenant, ct);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);

        var messages = await db.OutboxMessages.AsNoTracking()
            .Where(message => message.UserId == owner.Id || message.UserId == secondOwner.Id
                || message.UserId == retired.Id || message.UserId == suspended.Id).ToArrayAsync(Ct);
        Assert.Equal(2, messages.Length);
        Assert.DoesNotContain(messages, message => message.UserId == retired.Id);
        Assert.DoesNotContain(messages, message => message.UserId == suspended.Id);
        var protector = services.GetRequiredService<IPayloadProtector>();
        foreach (var message in messages)
        {
            var payload = protector.Unprotect(message.EncryptedPayload);
            Assert.DoesNotContain("INTERNAL-ONLY-REASON", payload);
            Assert.DoesNotContain(foreign.Value, payload);
            Assert.DoesNotContain(secondary.Value, payload);
            Assert.Contains(message.UserId == owner.Id ? fallback.Value : primary.Value, payload);
        }
    }

    private static TenantSettings Settings() => TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS");

    private static LoginMethod Method(Guid userId, bool primary, TimeProvider clock)
    {
        var method = LoginMethod.CreateEmail(userId, Email.Create(Guid.NewGuid().ToString("N") + "@example.test").Value);
        method.Verify(clock.GetUtcNow().UtcDateTime);
        if (primary) method.MakePrimary();
        return method;
    }
}
