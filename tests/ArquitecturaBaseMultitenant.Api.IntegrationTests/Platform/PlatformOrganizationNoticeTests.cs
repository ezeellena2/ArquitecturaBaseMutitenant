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
    [InlineData("rejected", "es-AR")]
    [InlineData("moderated", "es-AR")]
    [InlineData("approved", "es-AR")]
    [InlineData("approved", "en-US")]
    public async Task Organization_notices_reach_only_active_owners_safe_contacts_without_internal_reason(string action, string culture)
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var work = services.GetRequiredService<IUnitOfWork>();
        var clock = services.GetRequiredService<TimeProvider>();
        var owner = ApplicationUser.Create(null, culture, "America/Argentina/Buenos_Aires").Value;
        var secondOwner = ApplicationUser.Create(null, culture, "America/Argentina/Buenos_Aires").Value;
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
                services.GetRequiredService<IRoleAssignmentRepository>().Add(RoleAssignment.Assign(second.Id, role.Id, role.Scope, role.CompanyId, null).Value);
                var inactive = await members.GetByUserIdAsync(suspended.Id, ct);
                Assert.NotNull(inactive);
                services.GetRequiredService<IRoleAssignmentRepository>().Add(RoleAssignment.Assign(inactive.Id, role.Id, role.Scope, role.CompanyId, null).Value);
                Assert.True(former.Remove().IsSuccess);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        await work.ExecuteInTransactionAsync(async ct =>
        {
            var notifier = services.GetRequiredService<PlatformTenantNotifier>();
            if (action == "moderated") await notifier.EnqueuePageModerationAsync(tenant, true, ct);
            else if (action == "approved") await notifier.EnqueueApprovedAsync(tenant, owner.Id, ct);
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
            if (action == "approved") Assert.Contains(culture == "es-AR" ? "Aprobamos tu" : "We approved your", payload);
        }
    }

    private static TenantSettings Settings() => TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Approval_reaches_a_verified_secondary_email_of_the_organization_when_primary_is_foreign(
        bool invitationOrigin)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var work = services.GetRequiredService<IUnitOfWork>();
        var clock = services.GetRequiredService<TimeProvider>();
        var owner = ApplicationUser.Create(null, "es-AR", "UTC").Value;
        var tenant = Tenant.CreateBusiness("Aprobación " + Guid.NewGuid().ToString("N"), false);
        Assert.True(tenant.Activate().IsSuccess);
        var other = Tenant.CreateBusiness("Correo ajeno " + Guid.NewGuid().ToString("N"), false);
        Assert.True(other.Activate().IsSuccess);
        var foreign = Method(owner.Id, true, clock);
        foreign.AssignManagedByOrganization(other.Id);
        var address = Email.Create(Guid.NewGuid().ToString("N") + "@example.test").Value;
        var secondary = invitationOrigin
            ? LoginMethod.CreateInvitedEmail(owner.Id, address, tenant.Id)
            : LoginMethod.CreateEmail(owner.Id, address);
        Assert.True(secondary.Verify(clock.GetUtcNow().UtcDateTime).IsSuccess);
        if (!invitationOrigin) secondary.AssignManagedByOrganization(tenant.Id);
        var unverified = LoginMethod.CreateEmail(owner.Id,
            Email.Create(Guid.NewGuid().ToString("N") + "@example.test").Value);
        await work.ExecuteInTransactionAsync(_ =>
        {
            db.Users.Add(owner);
            db.LoginMethods.AddRange(foreign, secondary, unverified);
            services.GetRequiredService<ITenantRepository>().Add(other);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        using (services.GetRequiredService<ITenantScope>().Enter(tenant.Id))
            await work.ExecuteInTransactionAsync(_ =>
            {
                services.GetRequiredService<TenantSpaceProvisioner>().Stage(tenant, Settings(), [owner.Id], owner.Id);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        await work.ExecuteInTransactionAsync(async ct =>
        {
            // El Dueño preparado también está en el índice: la unión no duplica su correo.
            await services.GetRequiredService<PlatformTenantNotifier>().EnqueueApprovedAsync(tenant, owner.Id, ct);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        var message = Assert.Single(await db.OutboxMessages.AsNoTracking()
            .Where(item => item.UserId == owner.Id).ToArrayAsync(Ct));
        Assert.Equal("email", message.Channel);
        var payload = services.GetRequiredService<IPayloadProtector>().Unprotect(message.EncryptedPayload);
        Assert.Contains(address.Value, payload);
        Assert.DoesNotContain(foreign.Value, payload);
        Assert.DoesNotContain(unverified.Value, payload);
        Assert.Contains("Aprobamos tu", payload);
    }

    private static LoginMethod Method(Guid userId, bool primary, TimeProvider clock)
    {
        var method = LoginMethod.CreateEmail(userId, Email.Create(Guid.NewGuid().ToString("N") + "@example.test").Value);
        method.Verify(clock.GetUtcNow().UtcDateTime);
        if (primary) method.MakePrimary();
        return method;
    }
}
