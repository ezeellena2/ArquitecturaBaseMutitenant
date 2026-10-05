using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Platform;

[Collection(ApiTestGroup.Name)]
public sealed class PlatformTenantAdministrationTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Tenant_routes_reject_anonymous_access()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        foreach (var path in new[] { "paged", "filter-counts", Guid.NewGuid().ToString("D") })
        {
            using var response = await client.GetAsync("/api/platform/tenants/" + path, Ct);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        foreach (var action in new[] { "reject", "suspend", "reactivate", "close" })
        {
            using var response = await PlatformJourney.PostAsync(client, $"/api/platform/tenants/{Guid.NewGuid():D}/{action}", new { reason = "No autorizado", version = 1u }, Ct);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task Owner_lists_business_only_with_stable_pagination_and_shared_filter_counts()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await GrantAsync(factory, account.UserId, PlatformOperators.All);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        var prefix = "Listado" + Guid.NewGuid().ToString("N")[..8];
        var first = await SeedBusinessAsync(factory, prefix + " Álamo", false, account.UserId);
        await SeedBusinessAsync(factory, prefix + " Beta", true, account.UserId);
        using var list = await client.GetAsync("/api/platform/tenants/paged?page=1&pageSize=10&search=" + prefix, Ct);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var page = JsonDocument.Parse(await list.Content.ReadAsStringAsync(Ct));
        Assert.Equal(2, page.RootElement.GetProperty("totalCount").GetInt32());
        var items = page.RootElement.GetProperty("items");
        Assert.Equal(first, items[0].GetProperty("id").GetGuid());
        Assert.Equal("SelfService", items[0].GetProperty("source").GetString());
        Assert.Equal(account.Email.Value, items[0].GetProperty("ownerEmail").GetString());
        using var byEmail = await client.GetAsync("/api/platform/tenants/paged?search=" + Uri.EscapeDataString(account.Email.Value), Ct);
        using var emailJson = JsonDocument.Parse(await byEmail.Content.ReadAsStringAsync(Ct));
        Assert.Equal(2, emailJson.RootElement.GetProperty("totalCount").GetInt32());
        using var count = await client.GetAsync("/api/platform/tenants/filter-counts?search=" + prefix + "&status=PendingApproval", Ct);
        using var counts = JsonDocument.Parse(await count.Content.ReadAsStringAsync(Ct));
        Assert.Equal(1, counts.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(2, counts.RootElement.GetProperty("statuses").GetArrayLength());
        using var beyond = await client.GetAsync("/api/platform/tenants/paged?page=2&pageSize=10&search=" + prefix, Ct);
        using var beyondJson = JsonDocument.Parse(await beyond.Content.ReadAsStringAsync(Ct));
        Assert.Equal(0, beyondJson.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(2, beyondJson.RootElement.GetProperty("totalCount").GetInt32());
        using var escaped = await client.GetAsync("/api/platform/tenants/paged?search=%25", Ct);
        using var escapedJson = JsonDocument.Parse(await escaped.Content.ReadAsStringAsync(Ct));
        Assert.Equal(0, escapedJson.RootElement.GetProperty("totalCount").GetInt32());
        using var personal = await client.GetAsync($"/api/platform/tenants/{await PersonalIdAsync(account.UserId):D}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, personal.StatusCode);
    }

    [Fact]
    public async Task Changes_are_versioned_audited_and_keep_personal_available()
    {
        using var ownerClient = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        await GrantAsync(factory, owner.UserId, PlatformOperators.All);
        await AccountJourney.AuthorizeAsync(ownerClient, "platform", Ct);
        using var consumerClient = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var member = await AccountJourney.RegisterAsync(factory, consumerClient, Ct);
        var id = await SeedBusinessAsync(factory, "Estado " + Guid.NewGuid().ToString("N")[..8], false, member.UserId);
        await SeedMembershipAsync(id, member.UserId);
        var personalAuthorization = consumerClient.DefaultRequestHeaders.Authorization;
        await AccountJourney.AuthorizeAsync(consumerClient, "business", Ct, id);
        using var businessBefore = await consumerClient.GetAsync("/api/me", Ct);
        Assert.Equal(HttpStatusCode.OK, businessBefore.StatusCode);
        var noticesBefore = await OutboxCountAsync(member.UserId);
        var original = await VersionAsync(ownerClient, id);
        using var invalid = await PlatformJourney.PostAsync(ownerClient, $"/api/platform/tenants/{id:D}/suspend", new { version = original, reason = " " }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var suspended = await PlatformJourney.PostAsync(ownerClient, $"/api/platform/tenants/{id:D}/suspend", new { version = original, reason = "Revisión de actividad" }, Ct);
        var suspendedVersion = await SavedVersion.ReadAsync(suspended, Ct);
        Assert.NotEqual(original, suspendedVersion);
        Assert.Equal(suspendedVersion, await VersionAsync(ownerClient, id));
        Assert.Equal(TenantStatus.Suspended, (await StoredTenantAsync(factory, id)).Status);
        using var businessAfter = await consumerClient.GetAsync("/api/me", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, businessAfter.StatusCode);
        consumerClient.DefaultRequestHeaders.Authorization = personalAuthorization;
        using var me = await consumerClient.GetAsync("/api/me", Ct);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using var stale = await PlatformJourney.PostAsync(ownerClient, $"/api/platform/tenants/{id:D}/reactivate", new { version = original, reason = "Versión anterior" }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(TenantStatus.Suspended, (await StoredTenantAsync(factory, id)).Status);
        using var reactivated = await PlatformJourney.PostAsync(ownerClient, $"/api/platform/tenants/{id:D}/reactivate", new { version = suspendedVersion, reason = "Revisión terminada" }, Ct);
        var reactivatedVersion = await SavedVersion.ReadAsync(reactivated, Ct);
        Assert.Equal(reactivatedVersion, await VersionAsync(ownerClient, id));
        using var closeActive = await PlatformJourney.PostAsync(ownerClient, $"/api/platform/tenants/{id:D}/close", new { version = reactivatedVersion, reason = "Transición inválida" }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, closeActive.StatusCode);
        using var suspendAgain = await PlatformJourney.PostAsync(ownerClient, $"/api/platform/tenants/{id:D}/suspend", new { version = reactivatedVersion, reason = "Preparar cierre" }, Ct);
        var nextVersion = await SavedVersion.ReadAsync(suspendAgain, Ct);
        Assert.Equal(nextVersion, await VersionAsync(ownerClient, id));
        using var closed = await PlatformJourney.PostAsync(ownerClient, $"/api/platform/tenants/{id:D}/close", new { version = nextVersion, reason = "Cierre solicitado" }, Ct);
        var closedVersion = await SavedVersion.ReadAsync(closed, Ct);
        Assert.Equal(closedVersion, await VersionAsync(ownerClient, id));
        using var afterClose = await PlatformJourney.PostAsync(ownerClient, $"/api/platform/tenants/{id:D}/reactivate",
            new { version = closedVersion, reason = "Estado cerrado" }, Ct);
        Assert.Equal(TenantErrors.InvalidTransitionCode, await ErrorCodeAsync(afterClose));
        Assert.Equal(TenantStatus.Closed, (await StoredTenantAsync(factory,id)).Status);
        var pending = await SeedBusinessAsync(factory, "Rechazo " + Guid.NewGuid().ToString("N")[..8], true, member.UserId);
        using var rejected = await PlatformJourney.PostAsync(ownerClient, $"/api/platform/tenants/{pending:D}/reject", new { version = await VersionAsync(ownerClient,pending), reason = "Solicitud no válida" }, Ct);
        var rejectedVersion = await SavedVersion.ReadAsync(rejected, Ct);
        Assert.Equal(rejectedVersion, await VersionAsync(ownerClient, pending));
        using var afterReject = await PlatformJourney.PostAsync(ownerClient, $"/api/platform/tenants/{pending:D}/reject",
            new { version = rejectedVersion, reason = "Estado resuelto" }, Ct);
        Assert.Equal(TenantErrors.InvalidTransitionCode, await ErrorCodeAsync(afterReject));
        Assert.Equal(TenantStatus.Closed, (await StoredTenantAsync(factory,pending)).Status);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(4, await context.SecurityEvents.CountAsync(e => e.TargetTenantId == id, Ct));
        Assert.True(await context.SecurityEvents.AnyAsync(e => e.TargetTenantId == pending && e.ActorId == owner.UserId
            && e.Type == Domain.Auditing.SecurityEventType.OrganizationRejected && e.Reason == "Solicitud no válida", Ct));
        // Suspender/reactivar/cerrar no tienen aviso diseñado; el registrante sin membresía tampoco lo recibe.
        Assert.Equal(noticesBefore, await context.OutboxMessages.CountAsync(e => e.UserId == member.UserId, Ct));
    }

    [Fact]
    public async Task Support_can_read_but_neither_support_nor_consumer_can_write()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var support = await AccountJourney.RegisterAsync(factory, client, Ct);
        var id = await SeedBusinessAsync(factory, "Soporte " + Guid.NewGuid().ToString("N")[..8], false, support.UserId);
        using var consumer = await client.GetAsync("/api/platform/tenants/paged", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, consumer.StatusCode);
        await GrantAsync(factory, support.UserId, PlatformOperators.Reduced);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        using var listed = await client.GetAsync("/api/platform/tenants/paged", Ct);
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        foreach (var action in new[] { "reject", "suspend", "reactivate", "close" })
        {
            using var response = await PlatformJourney.PostAsync(client, $"/api/platform/tenants/{id:D}/{action}", new { version = 1u, reason = "Sin permiso" }, Ct);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        using var missing = await client.GetAsync($"/api/platform/tenants/{Guid.NewGuid():D}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Adding_a_member_after_reading_the_organization_does_not_conflict_with_suspension()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await GrantAsync(factory, account.UserId, PlatformOperators.All);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        var id = await SeedBusinessAsync(factory, "Versión y conteo " + Guid.NewGuid().ToString("N")[..8], false, account.UserId);
        var original = await VersionAsync(client, id);
        await SeedMembershipAsync(id, account.UserId);

        using var suspended = await PlatformJourney.PostAsync(client, $"/api/platform/tenants/{id:D}/suspend",
            new { version = original, reason = "Suspender luego del alta del miembro" }, Ct);
        Assert.Equal(HttpStatusCode.OK, suspended.StatusCode);
        using var detail = await client.GetAsync($"/api/platform/tenants/{id:D}", Ct);
        using var json = JsonDocument.Parse(await detail.Content.ReadAsStringAsync(Ct));
        Assert.Equal(1, json.RootElement.GetProperty("memberCount").GetInt32());
        Assert.Equal("Suspended", json.RootElement.GetProperty("status").GetString());
        Assert.NotEqual(original, json.RootElement.GetProperty("version").GetUInt32());

        using var stale = await PlatformJourney.PostAsync(client, $"/api/platform/tenants/{id:D}/reactivate",
            new { version = original, reason = "Reactivar con estado viejo" }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(TenantStatus.Suspended, (await StoredTenantAsync(factory, id)).Status);
    }

    private async Task<Guid> PersonalIdAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var rows = await scope.ServiceProvider.GetRequiredService<IUserTenantAccessReader>().ListForUserAsync(userId, Ct);
        return rows.Single(r => r.Kind == TenantKind.Personal).TenantId;
    }

    private async Task<int> OutboxCountAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .OutboxMessages.CountAsync(message => message.UserId == userId, Ct);
    }

    private async Task SeedMembershipAsync(Guid tenantId, Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var entered = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            var member = Member.Invite(userId);
            Assert.True(member.Activate(services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime).IsSuccess);
            services.GetRequiredService<IMemberRepository>().Add(member);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
    }

    [Fact]
    public async Task Global_counts_follow_private_writes_without_exposing_private_rows()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var user = await AccountJourney.RegisterAsync(factory, client, Ct);
        var id = await SeedBusinessAsync(factory, "Conteos " + Guid.NewGuid().ToString("N")[..8], false, user.UserId);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        Assert.False(await context.Members.AnyAsync(m => m.TenantId == id, Ct));
        Guid invitedId;
        using (services.GetRequiredService<ITenantScope>().Enter(id))
        {
            var invited = Member.Invite();
            invitedId = invited.Id;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                services.GetRequiredService<ICompanyRepository>().Add(Company.Create("Empresa del conteo", null, "America/Argentina/Buenos_Aires").Value);
                services.GetRequiredService<IMemberRepository>().Add(invited);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }
        Assert.False(await context.Members.AsNoTracking().AnyAsync(m => m.TenantId == id, Ct));
        Assert.False(await context.Companies.AsNoTracking().AnyAsync(c => c.TenantId == id, Ct));
        var global = await services.GetRequiredService<IPlatformReader>().FindTenantAsync(id, Ct);
        Assert.NotNull(global);
        Assert.Equal(1, global.CompanyCount);
        Assert.Equal(1, global.MemberCount);
        using (services.GetRequiredService<ITenantScope>().Enter(id))
        {
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var member = await context.Members.SingleAsync(m => m.Id == invitedId, ct);
                Assert.True(member.Remove().IsSuccess);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        Assert.Equal(0, (await services.GetRequiredService<IPlatformReader>().FindTenantAsync(id, Ct))!.MemberCount);
        Assert.Equal(1, (await services.GetRequiredService<IPlatformReader>().FindTenantAsync(id, Ct))!.CompanyCount);
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return json.RootElement.GetProperty("code").GetString();
    }

    private static async Task<uint> VersionAsync(HttpClient client, Guid id)
    {
        using var response = await client.GetAsync($"/api/platform/tenants/{id:D}", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return json.RootElement.GetProperty("version").GetUInt32();
    }

    private static Task GrantAsync(ApiFactory isolated, Guid userId, IReadOnlyCollection<string> permissions)
        => PlatformOperators.GrantAsync(isolated.Services, userId, permissions, Ct);

    private static async Task<Guid> SeedBusinessAsync(ApiFactory isolated, string name, bool pending, Guid ownerId)
    {
        await using var scope = isolated.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        return (await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync<Result<Guid>>(_ =>
        {
            var tenant = Tenant.CreateBusiness(name, pending);
            Assert.True(tenant.RecordRegistration(ownerId, TenantRegistrationSource.SelfService,
                services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime).IsSuccess);
            if (!pending) Assert.True(tenant.Activate().IsSuccess);
            services.GetRequiredService<ITenantRepository>().Add(tenant);
            return Task.FromResult<Result<Guid>>(tenant.Id);
        }, CommitPolicy.OnSuccess, Ct)).Value;
    }

    private static async Task<Tenant> StoredTenantAsync(ApiFactory isolated, Guid tenantId)
    {
        await using var scope = isolated.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Tenants.AsNoTracking().SingleAsync(t => t.Id == tenantId, Ct);
    }
}
