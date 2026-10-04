using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArquitecturaBaseMultitenant.Api.Contracts.Auth;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Platform;

[Collection(ApiTestGroup.Name)]
public sealed class PlatformTenantProvisioningHttpTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private HttpClient Client() => factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    [Fact]
    public async Task New_routes_require_operator_permissions()
    {
        using var client = Client();
        foreach (var path in new[] { "", $"/{Guid.NewGuid():D}/approve", $"/{Guid.NewGuid():D}/retry-provisioning" })
        {
            using var response = await client.PostAsJsonAsync("/api/platform/tenants" + path, new { }, Ct);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        var operatorAccount = await AccountJourney.RegisterAsync(factory, client, Ct);
        await GrantAsync(operatorAccount.UserId, PlatformOperators.Reduced);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        foreach (var path in new[] { "", $"/{Guid.NewGuid():D}/approve", $"/{Guid.NewGuid():D}/retry-provisioning" })
        {
            using var response = await client.PostAsJsonAsync("/api/platform/tenants" + path, new { }, Ct);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task Creation_invites_without_identity_and_acceptance_grants_owner_without_personal()
    {
        using var client = Client();
        var operatorAccount = await AccountJourney.RegisterAsync(factory, client, Ct);
        await GrantAsync(operatorAccount.UserId, PlatformOperators.All);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        var operationId = Guid.NewGuid();
        var email = Email.Create($"platform-invited-{Guid.NewGuid():N}@example.test").Value;
        var body = new { name = "Alta " + operationId.ToString("N")[..8], firstCompanyName = "Primera empresa", ownerEmail = email.Value, culture = "en-US", timeZoneId = "UTC" };
        var usersBefore = await CountUsersAsync();
        Guid[] previousOutbox;
        await using (var snapshot = factory.Services.CreateAsyncScope())
            previousOutbox = await snapshot.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutboxMessages.Select(o => o.Id).ToArrayAsync(Ct);
        using var first = await CreateAsync(client, operationId, body);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        using var created = JsonDocument.Parse(await first.Content.ReadAsStringAsync(Ct));
        Assert.Equal(operationId, created.RootElement.GetProperty("id").GetGuid());
        var invitationId = created.RootElement.GetProperty("invitationId").GetGuid();
        Assert.Equal(usersBefore, await CountUsersAsync());
        using var replay = await CreateAsync(client, operationId, body);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotent-Replayed")));
        // El tablero Organizaciones muestra el correo del Dueño desde el alta, aunque aún no tenga cuenta.
        using var pendingContact = await client.GetAsync($"/api/platform/tenants/{operationId:D}", Ct);
        using var pendingJson = JsonDocument.Parse(await pendingContact.Content.ReadAsStringAsync(Ct));
        Assert.Equal(email.Value, pendingJson.RootElement.GetProperty("ownerEmail").GetString());
        Assert.Equal(JsonValueKind.Null, pendingJson.RootElement.GetProperty("ownerName").ValueKind);
        using var byPendingEmail = await client.GetAsync("/api/platform/tenants/paged?search=" + Uri.EscapeDataString(email.Value), Ct);
        using var byPendingEmailJson = JsonDocument.Parse(await byPendingEmail.Content.ReadAsStringAsync(Ct));
        Assert.Equal(operationId, Assert.Single(byPendingEmailJson.RootElement.GetProperty("items").EnumerateArray())
            .GetProperty("id").GetGuid());

        string token;
        Guid memberId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var db = services.GetRequiredService<ApplicationDbContext>();
            using var tenant = services.GetRequiredService<ITenantScope>().Enter(operationId);
            Assert.Equal(2, await db.Roles.CountAsync(Ct));
            Assert.Equal(1, await db.Companies.CountAsync(Ct));
            var invitation = Assert.Single(await db.Invitations.AsNoTracking().ToListAsync(Ct));
            Assert.Equal(invitationId, invitation.Id);
            memberId = invitation.MemberId;
            var owner = await db.Members.AsNoTracking().SingleAsync(Ct);
            Assert.Null(owner.UserId);
            Assert.Equal(MemberStatus.Invited, owner.Status);
            var settings = await db.TenantSettings.AsNoTracking().SingleAsync(Ct);
            Assert.Equal("en-US", settings.DefaultCulture);
            var outbox = await db.OutboxMessages.AsNoTracking().Where(o => o.Channel == "email" && !previousOutbox.Contains(o.Id)).ToListAsync(Ct);
            var message = outbox.Select(o => JsonSerializer.Deserialize<EmailMessage>(services.GetRequiredService<IPayloadProtector>().Unprotect(o.EncryptedPayload))!)
                .Single(m => m.To == email.Value);
            // Alineación 1.4: como una invitación común, el correo lista Empresa y Roles (Dueño y Administrador).
            Assert.Contains("Company: Primera empresa", message.TextBody);
            Assert.Contains("Roles: Owner, Administrator", message.TextBody);
            token = Regex.Match(message.TextBody, @"/invitacion#(?<token>[A-Za-z0-9_-]+)").Groups["token"].Value;
            Assert.False(string.IsNullOrEmpty(token));
        }
        using var recipient = Client();
        using var csrfResponse = await recipient.GetAsync("/api/auth/external/google/antiforgery", Ct);
        var csrf = await csrfResponse.Content.ReadFromJsonAsync<GoogleSignupAntiforgeryHttpResponse>(Ct);
        recipient.DefaultRequestHeaders.Add("RequestVerificationToken", csrf!.RequestToken);
        using var preview = await recipient.PostAsJsonAsync("/api/invitations/preview", new { token }, Ct);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        using var previewJson = JsonDocument.Parse(await preview.Content.ReadAsStringAsync(Ct));
        Assert.Equal("NoAccount", previewJson.RootElement.GetProperty("state").GetString());
        using var acceptanceRequest = new HttpRequestMessage(HttpMethod.Post, "/api/invitations/accept") { Content = JsonContent.Create(new { token, acceptedTerms = true }) };
        acceptanceRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        using var accepted = await recipient.SendAsync(acceptanceRequest, Ct);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        await using var check = factory.Services.CreateAsyncScope();
        var checkDb = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userId = (await checkDb.LoginMethods.SingleAsync(m => m.Value == email.Value, Ct)).UserId;
        Assert.DoesNotContain(await check.ServiceProvider.GetRequiredService<IUserTenantAccessReader>().ListForUserAsync(userId, Ct), a => a.Kind == TenantKind.Personal);
        using var active = check.ServiceProvider.GetRequiredService<ITenantScope>().Enter(operationId);
        var admin = await checkDb.Roles.SingleAsync(r => r.Name == SystemRoles.TenantAdmin, Ct);
        Assert.Contains(await checkDb.RoleAssignments.ToListAsync(Ct), a => a.MemberId == memberId && a.RoleId == admin.Id && a.CompanyId == null);
        Assert.Equal(MemberStatus.Active, (await checkDb.Members.AsNoTracking().SingleAsync(Ct)).Status);
        // Aceptada la invitación, el contacto sale de la identidad y no queda una copia del correo.
        Assert.False(await checkDb.PendingTenantOwners.AnyAsync(owner => owner.TenantId == operationId, Ct));
        using var ownerContact = await client.GetAsync($"/api/platform/tenants/{operationId:D}", Ct);
        using var ownerJson = JsonDocument.Parse(await ownerContact.Content.ReadAsStringAsync(Ct));
        Assert.Equal(email.Value, ownerJson.RootElement.GetProperty("ownerEmail").GetString());
    }

    [Theory]
    [InlineData(true, "approve")]
    [InlineData(false, "retry-provisioning")]
    public async Task Approve_and_retry_reuse_existing_provisioning(bool pendingApproval, string action)
    {
        using var client = Client();
        var operatorAccount = await AccountJourney.RegisterAsync(factory, client, Ct);
        await GrantAsync(operatorAccount.UserId, PlatformOperators.All);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        var tenant = Tenant.CreateBusiness("Reanudar alta", pendingApproval);
        await using (var setup = factory.Services.CreateAsyncScope())
        {
            var services = setup.ServiceProvider;
            using var active = services.GetRequiredService<ITenantScope>().Enter(tenant.Id);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                Assert.True(tenant.RecordRegistration(operatorAccount.UserId, TenantRegistrationSource.SelfService, services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime).IsSuccess);
                var company = Company.Create("Empresa inicial", null, "UTC").Value;
                services.GetRequiredService<ICompanyRepository>().Add(company);
                return await services.GetRequiredService<TenantProvisioner>().StageAsync(tenant, TenantSettings.Create("es-AR", "UTC", "ARS"), [operatorAccount.UserId], operatorAccount.UserId, ct, company.Id);
            }, CommitPolicy.OnSuccess, Ct);
        }
        using var current = await client.GetAsync($"/api/platform/tenants/{tenant.Id:D}", Ct);
        using var metadata = JsonDocument.Parse(await current.Content.ReadAsStringAsync(Ct));
        var version = metadata.RootElement.GetProperty("version").GetUInt32();
        object body = pendingApproval ? new { version, reason = "Alta revisada" } : new { version };
        using var result = await PlatformJourney.PostAsync(client, $"/api/platform/tenants/{tenant.Id:D}/{action}", body, Ct);
        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
        using var stale = await PlatformJourney.PostAsync(client, $"/api/platform/tenants/{tenant.Id:D}/retry-provisioning", new { version, reason = "Repetir" }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        await using var check = factory.Services.CreateAsyncScope();
        var servicesCheck = check.ServiceProvider;
        var context = servicesCheck.GetRequiredService<ApplicationDbContext>();
        if (!pendingApproval)
            Assert.True(await context.SecurityEvents.AnyAsync(ev => ev.TargetTenantId == tenant.Id
                && ev.Type == Domain.Auditing.SecurityEventType.OrganizationProvisioningRetried
                && ev.Reason == "Reintentar el alta", Ct));
        Assert.Equal(TenantStatus.Active, (await context.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenant.Id, Ct)).Status);
        using var privateScope = servicesCheck.GetRequiredService<ITenantScope>().Enter(tenant.Id);
        Assert.Equal(2, await context.Roles.CountAsync(Ct));
        Assert.Equal(2, await context.RoleAssignments.CountAsync(Ct));
        Assert.Equal(1, await context.Members.CountAsync(Ct));
        Assert.Equal(1, await context.Companies.CountAsync(Ct));
        Assert.Equal(1, await context.TenantSettings.CountAsync(Ct));
    }

    [Fact]
    public async Task Retry_repairs_missing_owner_roles_and_settings_from_the_registered_identity()
    {
        using var client = Client();
        var operatorAccount = await AccountJourney.RegisterAsync(factory, client, Ct);
        await GrantAsync(operatorAccount.UserId, PlatformOperators.All);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        var tenant = Tenant.CreateBusiness("Alta incompleta", false);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                Assert.True(tenant.RecordRegistration(operatorAccount.UserId, TenantRegistrationSource.SelfService, services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime).IsSuccess);
                services.GetRequiredService<ITenantRepository>().Add(tenant);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }
        using var metadata = await client.GetAsync($"/api/platform/tenants/{tenant.Id:D}", Ct);
        using var json = JsonDocument.Parse(await metadata.Content.ReadAsStringAsync(Ct));
        using var repaired = await PlatformJourney.PostAsync(client, $"/api/platform/tenants/{tenant.Id:D}/retry-provisioning", new { version = json.RootElement.GetProperty("version").GetUInt32(), reason = "Completar el alta interrumpida" }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, repaired.StatusCode);
        await using var check = factory.Services.CreateAsyncScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        using var active = check.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenant.Id);
        Assert.Equal(operatorAccount.UserId, (await db.Members.AsNoTracking().SingleAsync(Ct)).UserId);
        Assert.Equal(2, await db.Roles.CountAsync(Ct));
        Assert.Equal(1, await db.RoleAssignments.CountAsync(Ct));
        Assert.Equal(1, await db.TenantSettings.CountAsync(Ct));
        Assert.Equal(TenantStatus.Active, (await db.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenant.Id, Ct)).Status);
        using var refreshed = await client.GetAsync($"/api/platform/tenants/{tenant.Id:D}", Ct);
        using var refreshedJson = JsonDocument.Parse(await refreshed.Content.ReadAsStringAsync(Ct));
        using var repeated = await PlatformJourney.PostAsync(client, $"/api/platform/tenants/{tenant.Id:D}/retry-provisioning", new { version = refreshedJson.RootElement.GetProperty("version").GetUInt32(), reason = "Comprobar reparación" }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, repeated.StatusCode);
        Assert.Equal(1, await db.Members.AsNoTracking().CountAsync(Ct));
        Assert.Equal(2, await db.Roles.AsNoTracking().CountAsync(Ct));
    }

    [Fact]
    public async Task Invalid_creation_and_unrecoverable_metadata_do_not_leave_partial_changes()
    {
        using var client = Client();
        var operatorAccount = await AccountJourney.RegisterAsync(factory, client, Ct);
        await GrantAsync(operatorAccount.UserId, PlatformOperators.All);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        var id = Guid.NewGuid();
        using var invalid = await CreateAsync(client, id, new { name = "Falta empresa", firstCompanyName = "", ownerEmail = "not-an-email", culture = "es-AR", timeZoneId = "UTC" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var tenant = Tenant.CreateBusiness("Sin responsable recuperable", false);
        await using (var setup = factory.Services.CreateAsyncScope())
        {
            var services = setup.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                services.GetRequiredService<ITenantRepository>().Add(tenant);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }
        using var current = await client.GetAsync($"/api/platform/tenants/{tenant.Id:D}", Ct);
        using var metadata = JsonDocument.Parse(await current.Content.ReadAsStringAsync(Ct));
        using var failed = await PlatformJourney.PostAsync(client, $"/api/platform/tenants/{tenant.Id:D}/retry-provisioning", new { version = metadata.RootElement.GetProperty("version").GetUInt32(), reason = "Alta sin identidad" }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, failed.StatusCode);
        await using var check = factory.Services.CreateAsyncScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.Tenants.AnyAsync(t => t.Id == id, Ct));
        Assert.False(await db.SecurityEvents.AnyAsync(e => e.TargetTenantId == id, Ct));
        Assert.Equal(TenantStatus.Provisioning, (await db.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenant.Id, Ct)).Status);
        using var active = check.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenant.Id);
        Assert.Equal(0, await db.Roles.CountAsync(Ct));
        Assert.Equal(0, await db.TenantSettings.CountAsync(Ct));
    }

    [Fact]
    public async Task Approve_requires_idempotency_and_replays_without_another_event_or_notice()
    {
        using var client = Client();
        var operatorAccount = await AccountJourney.RegisterAsync(factory, client, Ct);
        await GrantAsync(operatorAccount.UserId, PlatformOperators.All);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        var tenant = Tenant.CreateBusiness("Aprobación repetida", true);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            using var active = services.GetRequiredService<ITenantScope>().Enter(tenant.Id);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
                services.GetRequiredService<TenantProvisioner>().StageAsync(tenant, TenantSettings.Create("es-AR", "UTC", "ARS"), [operatorAccount.UserId], operatorAccount.UserId, ct), CommitPolicy.OnSuccess, Ct);
        }
        using var current = await client.GetAsync($"/api/platform/tenants/{tenant.Id:D}", Ct);
        using var metadata = JsonDocument.Parse(await current.Content.ReadAsStringAsync(Ct));
        var body = new { version = metadata.RootElement.GetProperty("version").GetUInt32(), reason = "Alta comprobada" };
        var path = $"/api/platform/tenants/{tenant.Id:D}/approve";
        using var missingKey = await client.PostAsJsonAsync(path, body, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, missingKey.StatusCode);
        var key = Guid.NewGuid();
        using var first = await SendAsync(client, path, key, body);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        using var replay = await SendAsync(client, path, key, body);
        Assert.Equal(HttpStatusCode.NoContent, replay.StatusCode);
        Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotent-Replayed")));
        await using var check = factory.Services.CreateAsyncScope();
        Assert.Equal(1, await check.ServiceProvider.GetRequiredService<ApplicationDbContext>().SecurityEvents
            .CountAsync(e => e.TargetTenantId == tenant.Id && e.Type == Domain.Auditing.SecurityEventType.OrganizationApproved, Ct));
    }

    [Fact]
    public async Task Concurrent_retries_keep_one_state_change_and_one_set_of_private_rows()
    {
        using var client = Client();
        var operatorAccount = await AccountJourney.RegisterAsync(factory, client, Ct);
        await GrantAsync(operatorAccount.UserId, PlatformOperators.All);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        var tenant = Tenant.CreateBusiness("Alta concurrente", false);
        await using (var setup = factory.Services.CreateAsyncScope())
        {
            var services = setup.ServiceProvider;
            using var active = services.GetRequiredService<ITenantScope>().Enter(tenant.Id);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
                services.GetRequiredService<TenantProvisioner>().StageAsync(tenant, TenantSettings.Create("es-AR", "UTC", "ARS"), [operatorAccount.UserId], operatorAccount.UserId, ct), CommitPolicy.OnSuccess, Ct);
        }
        using var current = await client.GetAsync($"/api/platform/tenants/{tenant.Id:D}", Ct);
        using var metadata = JsonDocument.Parse(await current.Content.ReadAsStringAsync(Ct));
        var body = new { version = metadata.RootElement.GetProperty("version").GetUInt32(), reason = "Reintento simultáneo" };
        var path = $"/api/platform/tenants/{tenant.Id:D}/retry-provisioning";
        var responses = await Task.WhenAll(PlatformJourney.PostAsync(client, path, body, Ct), PlatformJourney.PostAsync(client, path, body, Ct));
        try
        {
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.NoContent);
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        }
        finally { foreach (var response in responses) response.Dispose(); }
        await using var check = factory.Services.CreateAsyncScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.SecurityEvents.CountAsync(e => e.TargetTenantId == tenant.Id && e.Type == Domain.Auditing.SecurityEventType.OrganizationProvisioningRetried, Ct));
        using var privateScope = check.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenant.Id);
        Assert.Equal(1, await db.Members.CountAsync(Ct));
        Assert.Equal(2, await db.Roles.CountAsync(Ct));
        Assert.Equal(1, await db.RoleAssignments.CountAsync(Ct));
        Assert.Equal(1, await db.TenantSettings.CountAsync(Ct));
    }

    [Fact]
    public async Task A_revoked_owner_invitation_leaves_no_copy_of_the_invited_email()
    {
        using var client = Client();
        var operatorAccount = await AccountJourney.RegisterAsync(factory, client, Ct);
        await GrantAsync(operatorAccount.UserId, PlatformOperators.All);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        var operationId = Guid.NewGuid();
        var email = Email.Create($"revoked-owner-{Guid.NewGuid():N}@example.test").Value;
        using var created = await CreateAsync(client, operationId, new
        {
            name = "Revocada " + operationId.ToString("N")[..8], firstCompanyName = "Primera empresa",
            ownerEmail = email.Value, culture = "es-AR", timeZoneId = "America/Argentina/Buenos_Aires",
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync(Ct));
        var invitationId = createdJson.RootElement.GetProperty("invitationId").GetGuid();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            Assert.True(await services.GetRequiredService<ApplicationDbContext>().PendingTenantOwners
                .AnyAsync(owner => owner.InvitationId == invitationId, Ct));
            using var tenant = services.GetRequiredService<ITenantScope>().Enter(operationId);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var invitation = await services.GetRequiredService<IInvitationRepository>().GetByIdAsync(invitationId, ct);
                Assert.NotNull(invitation);
                return invitation.Revoke(services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
            }, CommitPolicy.OnSuccess, Ct);
        }

        await using var check = factory.Services.CreateAsyncScope();
        Assert.False(await check.ServiceProvider.GetRequiredService<ApplicationDbContext>().PendingTenantOwners
            .AnyAsync(owner => owner.TenantId == operationId, Ct));
        using var contact = await client.GetAsync($"/api/platform/tenants/{operationId:D}", Ct);
        using var contactJson = JsonDocument.Parse(await contact.Content.ReadAsStringAsync(Ct));
        Assert.Equal(JsonValueKind.Null, contactJson.RootElement.GetProperty("ownerEmail").ValueKind);
    }

    private async Task<int> CountUsersAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.CountAsync(Ct);
    }
    private static Task<HttpResponseMessage> CreateAsync(HttpClient client, Guid operationId, object body) =>
        SendAsync(client, "/api/platform/tenants", operationId, body);
    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string path, Guid operationId, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", operationId.ToString("D"));
        return await client.SendAsync(request, Ct);
    }
    private Task GrantAsync(Guid userId, IReadOnlyCollection<string> permissions)
        => PlatformOperators.GrantAsync(factory.Services, userId, permissions, Ct);
}
