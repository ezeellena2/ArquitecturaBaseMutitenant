using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Platform;

[Collection(ApiTestGroup.Name)]
public sealed class PlatformTenantDetailTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Detail_uses_current_owner_assignments_identity_and_last_successful_sign_in_without_private_scope()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await PlatformOperators.GrantAsync(factory.Services, account.UserId, [PlatformPermissions.Tenants.Read], Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var work = services.GetRequiredService<IUnitOfWork>();
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        nowUtc = nowUtc.AddTicks(-(nowUtc.Ticks % TimeSpan.TicksPerMicrosecond));
        var named = ApplicationUser.Create("Dueña vigente", "es-AR", "UTC").Value;
        var unnamed = ApplicationUser.Create(null, "es-AR", "UTC").Value;
        unnamed.Email = "principal@example.test";
        Assert.True(unnamed.Suspend().IsSuccess);
        var retired = ApplicationUser.Create("Registrante retirado", "es-AR", "UTC").Value;
        var tenant = Tenant.CreateBusiness("Ficha " + Guid.NewGuid().ToString("N"), false);
        tenant.RecordRegistration(retired.Id, TenantRegistrationSource.SelfService, nowUtc);
        tenant.Activate();
        await work.ExecuteInTransactionAsync(_ =>
        {
            db.Users.AddRange(named, unnamed, retired);
            db.LoginAudits.AddRange(LoginAudit.Success(named.Id, LoginAuditMethod.Google, nowUtc.AddDays(-2)),
                LoginAudit.Success(named.Id, LoginAuditMethod.Code, nowUtc.AddDays(-1)),
                LoginAudit.Failure(named.Id, LoginAuditMethod.Google,
                    LoginCodeErrors.Invalid(2), nowUtc));
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        using (services.GetRequiredService<ITenantScope>().Enter(tenant.Id))
        {
            await work.ExecuteInTransactionAsync(_ =>
            {
                services.GetRequiredService<TenantSpaceProvisioner>().Stage(tenant,
                    TenantSettings.Create("es-AR", "UTC", "ARS"), [named.Id, unnamed.Id, retired.Id], named.Id);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
            await work.ExecuteInTransactionAsync(async ct =>
            {
                var role = await services.GetRequiredService<IRoleRepository>().GetByNameAsync(SystemRoles.TenantAdmin, ct);
                var members = services.GetRequiredService<IMemberRepository>();
                var second = await members.GetByUserIdAsync(unnamed.Id, ct);
                services.GetRequiredService<IRoleAssignmentRepository>().Add(
                    RoleAssignment.Assign(second!.Id, role!.Id, role.Scope, role.CompanyId, null).Value);
                (await members.GetByUserIdAsync(retired.Id, ct))!.Remove();
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        Assert.False(await db.Members.AnyAsync(member => member.TenantId == tenant.Id, Ct));
        var version = (await db.Tenants.AsNoTracking().SingleAsync(row => row.Id == tenant.Id, Ct)).Version;
        using var response = await client.GetAsync($"/api/platform/tenants/{tenant.Id:D}", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(version, json.RootElement.GetProperty("version").GetUInt32());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("lastStateChange").ValueKind);
        var owners = json.RootElement.GetProperty("owners").EnumerateArray().ToArray();
        Assert.Equal(2, owners.Length);
        Assert.DoesNotContain(owners, owner => owner.GetProperty("id").GetGuid() == retired.Id);
        Assert.DoesNotContain("Registrante retirado", await response.Content.ReadAsStringAsync(Ct));
        var first = Assert.Single(owners, owner => owner.GetProperty("id").GetGuid() == named.Id);
        Assert.Equal("Dueña vigente", first.GetProperty("displayName").GetString());
        Assert.Equal(nowUtc.AddDays(-1), first.GetProperty("lastSignInAtUtc").GetDateTime());
        Assert.Equal("Code", first.GetProperty("lastSignInMethod").GetString());
        var secondOwner = Assert.Single(owners, owner => owner.GetProperty("id").GetGuid() == unnamed.Id);
        Assert.Equal(JsonValueKind.Null, secondOwner.GetProperty("displayName").ValueKind);
        Assert.Equal("principal@example.test", secondOwner.GetProperty("email").GetString());
        Assert.Equal("Suspended", secondOwner.GetProperty("identityStatus").GetString());
        Assert.Equal("Active", secondOwner.GetProperty("membershipStatus").GetString());
        Assert.Equal(JsonValueKind.Null, secondOwner.GetProperty("lastSignInAtUtc").ValueKind);
        // La lectura global no entra a la organización ni escribe una entrada técnica.
        Assert.False(await db.SecurityEvents.AnyAsync(item => item.TargetTenantId == tenant.Id, Ct));
        await work.ExecuteInTransactionAsync(_ =>
        {
            Assert.True(unnamed.RequestDeletion("Solicitud", nowUtc.AddDays(-2), 1, account.UserId).IsSuccess);
            Assert.True(unnamed.CompleteDeletion(nowUtc, "Cuenta eliminada").IsSuccess);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        using var deletedResponse = await client.GetAsync($"/api/platform/tenants/{tenant.Id:D}", Ct);
        using var deletedJson = JsonDocument.Parse(await deletedResponse.Content.ReadAsStringAsync(Ct));
        var deleted = Assert.Single(deletedJson.RootElement.GetProperty("owners").EnumerateArray(),
            owner => owner.GetProperty("id").GetGuid() == unnamed.Id);
        Assert.Equal("Deleted", deleted.GetProperty("identityStatus").GetString());
        Assert.Equal("Cuenta eliminada", deleted.GetProperty("displayName").GetString());
        Assert.Equal(JsonValueKind.Null, deleted.GetProperty("email").ValueKind);
        Assert.Equal(version, deletedJson.RootElement.GetProperty("version").GetUInt32());
    }

    [Fact]
    public async Task History_filters_in_sql_paginates_tied_dates_and_last_state_requires_effective_status_evidence()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var missing = $"/api/platform/tenants/{Guid.NewGuid():D}/history";
        using var anonymous = await client.GetAsync(missing, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        using var consumer = await client.GetAsync(missing, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, consumer.StatusCode);
        await PlatformOperators.GrantAsync(factory.Services, account.UserId, [PlatformPermissions.Tenants.Read], Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        using var absent = await client.GetAsync(missing, Ct);
        Assert.Equal(HttpStatusCode.NotFound, absent.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        nowUtc = nowUtc.AddTicks(-(nowUtc.Ticks % TimeSpan.TicksPerMicrosecond));
        var tenant = Tenant.CreateBusiness("Historial " + Guid.NewGuid().ToString("N"), false);
        tenant.Activate();
        var other = Tenant.CreateBusiness("Otra organización", false);
        other.Activate();
        var personalId = await db.UserTenantAccesses.Where(access => access.UserId == account.UserId
                && db.Tenants.Any(item => item.Id == access.TenantId && item.Kind == TenantKind.Personal))
            .Select(access => access.TenantId).SingleAsync(Ct);
        var effective = new SecurityEventChanges();
        effective.AddEnum("status", TenantStatus.Active, TenantStatus.Suspended);
        var noChange = new SecurityEventChanges();
        noChange.AddEnum("status", TenantStatus.Active, TenantStatus.Active);
        var state = SecurityEvent.ForTenantAdministration(SecurityEventType.OrganizationSuspended, account.UserId,
            tenant.Id, "Revisión interna", nowUtc.AddDays(-1)).WithEvidence(effective, null);
        var events = new[]
        {
            state,
            SecurityEvent.ForTenantFeatures(AuditActorKind.PlatformOperator, account.UserId, tenant.Id, "Módulos", nowUtc),
            SecurityEvent.ForPlatformTenantAccess(account.UserId, tenant.Id, "Entrada técnica", nowUtc),
            SecurityEvent.ForTenantAdministration(SecurityEventType.OrganizationProvisioningRetried, account.UserId,
                tenant.Id, "Reintentar el alta", nowUtc).WithEvidence(noChange, null),
        };
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            services.GetRequiredService<ITenantRepository>().Add(tenant);
            services.GetRequiredService<ITenantRepository>().Add(other);
            db.SecurityEvents.AddRange(events);
            db.SecurityEvents.Add(SecurityEvent.ForTenantAdministration(SecurityEventType.OrganizationClosed,
                account.UserId, other.Id, "AJENA", nowUtc));
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        using var personal = await client.GetAsync($"/api/platform/tenants/{personalId:D}/history", Ct);
        Assert.Equal(HttpStatusCode.NotFound, personal.StatusCode);
        using var detail = await client.GetAsync($"/api/platform/tenants/{tenant.Id:D}", Ct);
        using var detailJson = JsonDocument.Parse(await detail.Content.ReadAsStringAsync(Ct));
        var last = detailJson.RootElement.GetProperty("lastStateChange");
        Assert.Equal(nowUtc.AddDays(-1), last.GetProperty("atUtc").GetDateTime());
        Assert.Equal("Revisión interna", last.GetProperty("reason").GetString());
        Assert.DoesNotContain("OrganizationSuspended", last.GetProperty("action").GetString()!);
        var found = new List<Guid>();
        string? after = null;
        do
        {
            var path = $"/api/platform/tenants/{tenant.Id:D}/history?limit=1"
                + (after is null ? "" : "&after=" + Uri.EscapeDataString(after));
            using var page = await client.GetAsync(path, Ct);
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            using var pageJson = JsonDocument.Parse(await page.Content.ReadAsStringAsync(Ct));
            var row = Assert.Single(pageJson.RootElement.GetProperty("items").EnumerateArray());
            found.Add(row.GetProperty("id").GetGuid());
            Assert.NotEqual("AJENA", row.GetProperty("reason").GetString());
            Assert.True(row.TryGetProperty("summary", out _));
            after = pageJson.RootElement.GetProperty("nextCursor").GetString();
        } while (after is not null);
        Assert.Equal(events.OrderByDescending(item => item.OccurredAtUtc).ThenByDescending(item => item.Id)
            .Select(item => item.Id), found);
        Assert.Equal(events.Length, found.Distinct().Count());
        using var invalid = await client.GetAsync($"/api/platform/tenants/{tenant.Id:D}/history?after=bad", Ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        await PlatformOperators.GrantAsync(factory.Services, account.UserId, [PlatformPermissions.Accounts.Read], Ct);
        using var noPermission = await client.GetAsync($"/api/platform/tenants/{tenant.Id:D}/history", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, noPermission.StatusCode);
    }
}
