using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Platform;

/// <summary>Consulta global de cuentas: estados derivados, tipos, búsqueda, conteos y ficha sin datos completos.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class PlatformAccountAdministrationTests(ApiFactory factory)
{
    private const string Zone = "America/Argentina/Buenos_Aires";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Account_routes_require_platform_access_and_accounts_read()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        foreach (var path in new[] { "paged", "filter-counts", Guid.NewGuid().ToString("D") })
        {
            using var anonymous = await client.GetAsync("/api/platform/accounts/" + path, Ct);
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        }

        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        using var consumer = await client.GetAsync("/api/platform/accounts/paged", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, consumer.StatusCode);

        using var operatorClient = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var operatorAccount = await AccountJourney.RegisterAsync(factory, operatorClient, Ct);
        await PlatformOperators.GrantAsync(factory.Services, operatorAccount.UserId, [PlatformPermissions.Tenants.Read], Ct);
        await AccountJourney.AuthorizeAsync(operatorClient, "platform", Ct);
        foreach (var path in new[] { "paged", "filter-counts", account.UserId.ToString("D") })
        {
            using var denied = await operatorClient.GetAsync("/api/platform/accounts/" + path, Ct);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
    }

    [Fact]
    public async Task Operator_lists_accounts_with_derived_states_kinds_search_and_shared_counts()
    {
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        using var client = isolated.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var viewer = await AccountJourney.RegisterAsync(isolated, client, Ct);
        await PlatformOperators.GrantAsync(isolated.Services, viewer.UserId, PlatformOperators.Reduced, Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        var seeded = await SeedAsync(isolated);

        var all = await GetAsync(client, "paged?page=1&pageSize=50");
        Assert.Equal(10, all.GetProperty("totalCount").GetInt32());
        Assert.Equal("Active", State(all, seeded.Active));
        Assert.Equal("Suspended", State(all, seeded.Suspended));
        Assert.Equal("PendingDeletion", State(all, seeded.PendingDeletion));
        Assert.Equal("Deleted", State(all, seeded.Deleted));
        Assert.Equal("NeedsRecovery", State(all, seeded.NeedsRecovery));
        Assert.Equal("Suspended", State(all, seeded.SuspendedWithoutMethods));
        Assert.Equal("Active", State(all, seeded.ManagedActive));
        // Solo un correo administrado, con membresía y organización activas: sirve para ingresar.
        Assert.Equal("Active", State(all, seeded.ManagedOnly));
        var active = Row(all, seeded.Active);
        Assert.Equal("Person", active.GetProperty("kind").GetString());
        Assert.True(active.GetProperty("hasPersonalSpace").GetBoolean());
        Assert.Equal(1, active.GetProperty("organizationCount").GetInt32());
        Assert.Equal(seeded.ActiveEmail, active.GetProperty("primaryEmail").GetString());
        Assert.Equal("Code", active.GetProperty("lastSignInMethod").GetString());
        Assert.Equal(seeded.LastSignInAtUtc, active.GetProperty("lastSignInAtUtc").GetDateTime().ToUniversalTime(),
            TimeSpan.FromMilliseconds(1));
        Assert.Equal("Operator", Row(all, seeded.Operator).GetProperty("kind").GetString());
        Assert.Equal(JsonValueKind.Null, Row(all, seeded.Deleted).GetProperty("primaryEmail").ValueKind);

        Assert.Equal([seeded.NeedsRecovery], Ids(await GetAsync(client, "paged?state=NeedsRecovery")));
        Assert.Equal(new[] { seeded.Suspended, seeded.SuspendedWithoutMethods }.Order(),
            Ids(await GetAsync(client, "paged?state=Suspended")).Order());
        Assert.Equal(new[] { seeded.Operator, viewer.UserId }.Order(), Ids(await GetAsync(client, "paged?kind=Operator")).Order());
        Assert.Equal(new[] { seeded.Active, seeded.ManagedActive, seeded.ManagedOnly }.Order(),
            Ids(await GetAsync(client, "paged?kind=Person&state=Active")).Order());

        Assert.Equal([seeded.Active], Ids(await GetAsync(client, "paged?search=alvaro")));
        Assert.Equal([seeded.ManagedActive], Ids(await GetAsync(client, "paged?search=" + Uri.EscapeDataString(seeded.SecondaryFragment))));
        Assert.Empty(Ids(await GetAsync(client, "paged?search=%25")));
        // Se busca por los métodos de ingreso: la copia Users.Email no encuentra nada (emails.md).
        Assert.Empty(Ids(await GetAsync(client, "paged?search=copia-vieja-" + seeded.Prefix)));
        Assert.Equal([seeded.Suspended], Ids(await GetAsync(client, "paged?search=beatriz.google")));

        var firstPage = await GetAsync(client, "paged?page=1&pageSize=10&kind=Person");
        Assert.Equal(8, firstPage.GetProperty("totalCount").GetInt32());
        Assert.Equal(8, firstPage.GetProperty("items").GetArrayLength());
        Assert.Equal("Álvaro " + seeded.Prefix, firstPage.GetProperty("items")[0].GetProperty("displayName").GetString());
        var beyond = await GetAsync(client, "paged?page=2&pageSize=10&kind=Person");
        Assert.Equal(8, beyond.GetProperty("totalCount").GetInt32());
        Assert.Equal(0, beyond.GetProperty("items").GetArrayLength());

        var counts = await GetAsync(client, "filter-counts?kind=Person&state=Suspended");
        Assert.Equal(2, counts.GetProperty("total").GetInt32());
        Assert.Equal(3, Count(counts, "states", "state", "Active"));
        Assert.Equal(2, Count(counts, "states", "state", "Suspended"));
        Assert.Equal(1, Count(counts, "states", "state", "NeedsRecovery"));
        Assert.Equal(1, Count(counts, "states", "state", "PendingDeletion"));
        Assert.Equal(1, Count(counts, "states", "state", "Deleted"));
        Assert.Equal(2, Count(counts, "kinds", "kind", "Person"));
        Assert.Equal(0, Count(counts, "kinds", "kind", "Operator"));
    }

    [Fact]
    public async Task Account_detail_shows_masked_methods_memberships_and_operator_permissions()
    {
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        using var client = isolated.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var viewer = await AccountJourney.RegisterAsync(isolated, client, Ct);
        await PlatformOperators.GrantAsync(isolated.Services, viewer.UserId, PlatformOperators.Reduced, Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        var seeded = await SeedAsync(isolated);

        using var activeResponse = await client.GetAsync($"/api/platform/accounts/{seeded.Active:D}", Ct);
        Assert.Equal(HttpStatusCode.OK, activeResponse.StatusCode);
        var activeJson = await activeResponse.Content.ReadAsStringAsync(Ct);
        Assert.DoesNotContain(seeded.ActiveEmail, activeJson);
        var active = JsonDocument.Parse(activeJson).RootElement;
        Assert.Equal("Active", active.GetProperty("state").GetString());
        Assert.Equal("Álvaro " + seeded.Prefix, active.GetProperty("displayName").GetString());
        var method = Assert.Single(active.GetProperty("loginMethods").EnumerateArray());
        Assert.Equal("al•••@" + seeded.Prefix + ".test", method.GetProperty("maskedValue").GetString());
        Assert.True(method.GetProperty("isPrimary").GetBoolean());
        Assert.True(method.GetProperty("canSignIn").GetBoolean());
        var membership = Assert.Single(active.GetProperty("organizations").EnumerateArray());
        Assert.Equal("Activa " + seeded.Prefix, membership.GetProperty("name").GetString());
        Assert.Equal(SystemRoles.TenantAdmin, membership.GetProperty("roleName").GetString());
        Assert.Equal("Active", membership.GetProperty("status").GetString());
        Assert.Equal(["Activa " + seeded.Prefix], active.GetProperty("soleOwnerOrganizations").EnumerateArray().Select(item => item.GetString()));
        Assert.True(active.GetProperty("hasPersonalSpace").GetBoolean());
        Assert.Equal(JsonValueKind.Null, active.GetProperty("operatorPermissions").ValueKind);
        Assert.Equal("Code", active.GetProperty("lastSignInMethod").GetString());
        Assert.NotEqual(0u, active.GetProperty("version").GetUInt32());

        var recovery = await GetAsync(client, seeded.NeedsRecovery.ToString("D"));
        Assert.Equal("NeedsRecovery", recovery.GetProperty("state").GetString());
        var managed = Assert.Single(recovery.GetProperty("loginMethods").EnumerateArray());
        Assert.Equal("Kappa " + seeded.Prefix, managed.GetProperty("managedByOrganizationName").GetString());
        Assert.False(managed.GetProperty("canSignIn").GetBoolean());
        var ended = Assert.Single(recovery.GetProperty("organizations").EnumerateArray());
        Assert.Equal("Inactive", ended.GetProperty("status").GetString());
        Assert.Equal(seeded.MembershipEndedAtUtc, ended.GetProperty("endedAtUtc").GetDateTime().ToUniversalTime(),
            TimeSpan.FromMilliseconds(1));

        var pending = await GetAsync(client, seeded.PendingDeletion.ToString("D"));
        var deletion = pending.GetProperty("deletion");
        Assert.False(deletion.GetProperty("requestedByPlatform").GetBoolean());
        Assert.True(deletion.GetProperty("scheduledForUtc").GetDateTime() > deletion.GetProperty("requestedAtUtc").GetDateTime());

        var operatorAccount = await GetAsync(client, seeded.Operator.ToString("D"));
        Assert.Equal("Operator", operatorAccount.GetProperty("kind").GetString());
        Assert.Equal(PlatformOperators.Reduced, operatorAccount.GetProperty("operatorPermissions").EnumerateArray()
            .Select(item => item.GetString()!));

        var deleted = await GetAsync(client, seeded.Deleted.ToString("D"));
        Assert.Equal("Deleted", deleted.GetProperty("state").GetString());
        Assert.Equal("Cuenta eliminada", deleted.GetProperty("displayName").GetString());
        Assert.Empty(deleted.GetProperty("loginMethods").EnumerateArray());

        // La fecha de alta la fija el registro; las cuentas sembradas sin fecha quedan sin evidencia.
        var registered = await GetAsync(client, viewer.UserId.ToString("D"));
        Assert.Equal(JsonValueKind.String, registered.GetProperty("createdAtUtc").ValueKind);
        Assert.Equal(JsonValueKind.Null, active.GetProperty("createdAtUtc").ValueKind);

        using var missing = await client.GetAsync($"/api/platform/accounts/{Guid.NewGuid():D}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    private sealed record Seeded(string Prefix, Guid Active, string ActiveEmail, DateTime LastSignInAtUtc,
        Guid Suspended, Guid PendingDeletion, Guid Deleted, Guid NeedsRecovery, DateTime MembershipEndedAtUtc,
        Guid SuspendedWithoutMethods, Guid ManagedActive, Guid ManagedOnly, string SecondaryFragment, Guid Operator);

    // Ocho personas y un operador además de quien consulta: cada estado derivado y sus prioridades.
    private static async Task<Seeded> SeedAsync(ApiFactory isolated)
    {
        var prefix = "c" + Guid.NewGuid().ToString("N")[..8];
        await using var scope = isolated.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var work = services.GetRequiredService<IUnitOfWork>();
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;

        var active = User("Álvaro " + prefix);
        var suspended = User("Beatriz " + prefix);
        Assert.True(suspended.Suspend().IsSuccess);
        var pending = User("Carla " + prefix);
        Assert.True(pending.RequestDeletion("Ya no uso el servicio.", now, 30).IsSuccess);
        var deleted = User("Damián " + prefix);
        Assert.True(deleted.RequestDeletion("Baja", now.AddDays(-31), 30).IsSuccess);
        Assert.True(deleted.CompleteDeletion(now, "Cuenta eliminada").IsSuccess);
        var recovery = User("Elena " + prefix);
        var suspendedWithoutMethods = User("Fabián " + prefix);
        Assert.True(suspendedWithoutMethods.Suspend().IsSuccess);
        var managedActive = User("Gabriela " + prefix);
        var managedOnly = User("Ignacio " + prefix);
        var operatorUser = User("Hugo " + prefix);
        operatorUser.GrantPlatformOperator();

        var activeOrganization = Organization("Activa " + prefix);
        var closedMembership = Organization("Kappa " + prefix);
        var activeEmail = "alvaro@" + prefix + ".test";
        var secondary = "gabí-" + prefix + "@personal.test";
        var methods = new[]
        {
            Method(active.Id, activeEmail, true, now, null),
            Method(suspended.Id, "beatriz@" + prefix + ".test", true, now, null),
            Method(pending.Id, "carla@" + prefix + ".test", true, now, null),
            Method(recovery.Id, "elena@" + prefix + ".test", true, now, closedMembership.Id),
            Method(suspendedWithoutMethods.Id, "fabian@" + prefix + ".test", true, now, closedMembership.Id),
            Method(managedActive.Id, "gabriela@" + prefix + ".test", true, now, activeOrganization.Id),
            Method(managedActive.Id, secondary, false, now, null),
            Method(managedOnly.Id, "ignacio@" + prefix + ".test", true, now, activeOrganization.Id),
            Method(operatorUser.Id, "hugo@" + prefix + ".test", true, now, null),
            LoginMethod.CreateGoogle(suspended.Id, "google-" + prefix,
                Email.Create("beatriz.google@" + prefix + ".test").Value),
        };
        // Users.Email es la copia del método principal, como la mantiene la cuenta.
        foreach (var user in new[] { active, suspended, pending, recovery, suspendedWithoutMethods, managedActive, managedOnly, operatorUser })
            user.Email = methods.Single(method => method.UserId == user.Id && method.IsPrimary).Value;
        // Una copia que ya no coincide con ningún método.
        suspendedWithoutMethods.Email = "copia-vieja-" + prefix + "@example.test";
        var lastSignIn = now.AddMinutes(-55);
        await work.ExecuteInTransactionAsync(_ =>
        {
            db.Users.AddRange(active, suspended, pending, deleted, recovery, suspendedWithoutMethods, managedActive, managedOnly, operatorUser);
            db.LoginMethods.AddRange(methods);
            db.LoginAudits.Add(LoginAudit.Success(active.Id, LoginAuditMethod.Code, lastSignIn.AddDays(-1)));
            db.LoginAudits.Add(LoginAudit.Success(active.Id, LoginAuditMethod.Code, lastSignIn));
            db.LoginAudits.Add(LoginAudit.Failure(active.Id, LoginAuditMethod.Google,
                Error.Validation("Identity.Login.Failed", "Fallido"), now));
            db.PlatformPermissionAssignments.Add(PlatformPermissionAssignment.Create(operatorUser.Id, PlatformOperators.Reduced).Value);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);

        var provisioner = services.GetRequiredService<TenantSpaceProvisioner>();
        var personal = await services.GetRequiredService<IPersonalSpaceProvisioner>().PrepareAsync("es-AR", Zone, Ct);
        using (services.GetRequiredService<ITenantScope>().Enter(personal.Tenant.Id))
            await work.ExecuteInTransactionAsync(_ =>
            {
                services.GetRequiredService<IPersonalSpaceProvisioner>().Stage(personal, active.Id);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        using (services.GetRequiredService<ITenantScope>().Enter(activeOrganization.Id))
            await work.ExecuteInTransactionAsync(_ =>
            {
                provisioner.Stage(activeOrganization, Settings(), [active.Id, managedActive.Id, managedOnly.Id], active.Id);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        using (services.GetRequiredService<ITenantScope>().Enter(closedMembership.Id))
        {
            await work.ExecuteInTransactionAsync(_ =>
            {
                provisioner.Stage(closedMembership, Settings(), [recovery.Id, suspendedWithoutMethods.Id], recovery.Id);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
            await work.ExecuteInTransactionAsync(async ct =>
            {
                var members = services.GetRequiredService<IMemberRepository>();
                foreach (var userId in new[] { recovery.Id, suspendedWithoutMethods.Id })
                {
                    var member = await members.GetByUserIdAsync(userId, ct);
                    Assert.NotNull(member);
                    Assert.True(member.Deactivate().IsSuccess);
                }
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        var endedAt = now.AddDays(-2);
        await work.ExecuteInTransactionAsync(_ =>
        {
            services.GetRequiredService<ISecurityEventRepository>().Add(SecurityEvent.ForOrganizationMembershipEnded(
                active.Id, closedMembership.Id, recovery.Id, endedAt));
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);

        return new Seeded(prefix, active.Id, activeEmail, lastSignIn, suspended.Id, pending.Id, deleted.Id,
            recovery.Id, endedAt, suspendedWithoutMethods.Id, managedActive.Id, managedOnly.Id, "gabi-" + prefix, operatorUser.Id);
    }

    private static ApplicationUser User(string name) => ApplicationUser.Create(name, "es-AR", Zone).Value;

    private static Tenant Organization(string name)
    {
        var tenant = Tenant.CreateBusiness(name, false);
        Assert.True(tenant.Activate().IsSuccess);
        return tenant;
    }

    private static LoginMethod Method(Guid userId, string email, bool primary, DateTime now, Guid? managedBy)
    {
        var method = LoginMethod.CreateEmail(userId, Email.Create(email).Value);
        method.Verify(now);
        if (primary) Assert.True(method.MakePrimary().IsSuccess);
        if (managedBy is { } tenantId) method.AssignManagedByOrganization(tenantId);
        return method;
    }

    private static TenantSettings Settings() => TenantSettings.Create("es-AR", Zone, "ARS");

    private static async Task<JsonElement> GetAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync("/api/platform/accounts/" + path, Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement.Clone();
    }

    private static JsonElement Row(JsonElement page, Guid id) =>
        page.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == id);

    private static string? State(JsonElement page, Guid id) => Row(page, id).GetProperty("state").GetString();

    private static Guid[] Ids(JsonElement page) =>
        [.. page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())];

    private static int Count(JsonElement counts, string dimension, string key, string value) =>
        counts.GetProperty(dimension).EnumerateArray()
            .Where(item => item.GetProperty(key).GetString() == value)
            .Select(item => item.GetProperty("count").GetInt32()).SingleOrDefault();
}
