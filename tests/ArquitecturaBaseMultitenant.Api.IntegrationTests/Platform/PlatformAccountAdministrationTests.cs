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
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Platform;

/// <summary>Consulta global de cuentas: estados derivados, correo principal, conteos y métodos enmascarados.</summary>
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
        Assert.All(all.GetProperty("items").EnumerateArray(), item => Assert.False(item.TryGetProperty("version", out _)));
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
        var active = JsonDocument.Parse(activeJson).RootElement;
        Assert.DoesNotContain(seeded.ActiveEmail, active.GetProperty("loginMethods").GetRawText());
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
        Assert.False(active.TryGetProperty("version", out _));
        Assert.Equal(JsonValueKind.Null, active.GetProperty("lastStateChange").ValueKind);
        Assert.Equal(JsonValueKind.Null, active.GetProperty("deletedAtUtc").ValueKind);

        var recovery = await GetAsync(client, seeded.NeedsRecovery.ToString("D"));
        Assert.Equal("NeedsRecovery", recovery.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, recovery.GetProperty("lastStateChange").ValueKind);
        var suspended = await GetAsync(client, seeded.Suspended.ToString("D"));
        Assert.Equal(JsonValueKind.Null, suspended.GetProperty("lastStateChange").ValueKind);
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
        Assert.False(operatorAccount.TryGetProperty("version", out _));
        Assert.NotEqual(0u, operatorAccount.GetProperty("operatorVersion").GetUInt32());
        Assert.Equal(PlatformOperators.Reduced, operatorAccount.GetProperty("operatorPermissions").EnumerateArray()
            .Select(item => item.GetString()!));

        var deleted = await GetAsync(client, seeded.Deleted.ToString("D"));
        Assert.Equal("Deleted", deleted.GetProperty("state").GetString());
        Assert.Equal("Cuenta eliminada", deleted.GetProperty("displayName").GetString());
        Assert.Equal(JsonValueKind.Null, deleted.GetProperty("primaryEmail").ValueKind);
        Assert.Empty(deleted.GetProperty("loginMethods").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, deleted.GetProperty("lastStateChange").ValueKind);
        await using var scope = isolated.Services.CreateAsyncScope();
        var persistedDeletedAtUtc = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users
            .Where(user => user.Id == seeded.Deleted).Select(user => user.DeletedAtUtc).SingleAsync(Ct);
        Assert.Equal(persistedDeletedAtUtc, deleted.GetProperty("deletedAtUtc").GetDateTime());
        Assert.EndsWith("Z", deleted.GetProperty("deletedAtUtc").GetString());

        // La fecha de alta la fija el registro; las cuentas sembradas sin fecha quedan sin evidencia.
        var registered = await GetAsync(client, viewer.UserId.ToString("D"));
        Assert.Equal(JsonValueKind.String, registered.GetProperty("createdAtUtc").ValueKind);
        Assert.Equal(JsonValueKind.Null, active.GetProperty("createdAtUtc").ValueKind);

        using var missing = await client.GetAsync($"/api/platform/accounts/{Guid.NewGuid():D}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Account_detail_returns_the_list_primary_email_to_a_read_only_operator(bool invitedOperator)
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var viewer = await AccountJourney.RegisterAsync(factory, client, Ct);
        await PlatformOperators.GrantAsync(factory.Services, viewer.UserId, [PlatformPermissions.Accounts.Read], Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var prefix = Guid.NewGuid().ToString("N");
        var email = "principal-" + prefix + "@example.test";
        var googleEmail = "contacto-" + prefix + "@example.test";
        var account = ApplicationUser.Create(invitedOperator ? null : "Persona " + prefix, "es-AR", Zone).Value;
        account.Email = email;
        if (invitedOperator) account.InvitePlatformOperator(nowUtc.AddDays(7));
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            db.Users.Add(account);
            db.LoginMethods.Add(Method(account.Id, email, true, nowUtc, null));
            db.LoginMethods.Add(LoginMethod.CreateGoogle(account.Id, "google-" + prefix, Email.Create(googleEmail).Value));
            if (invitedOperator)
                db.PlatformPermissionAssignments.Add(PlatformPermissionAssignment.Create(account.Id,
                    [PlatformPermissions.Accounts.Read]).Value);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);

        var row = Row(await GetAsync(client, "paged?search=" + Uri.EscapeDataString(email)), account.Id);
        var detail = await GetAsync(client, account.Id.ToString("D"));
        Assert.Equal(email, row.GetProperty("primaryEmail").GetString());
        Assert.Equal(row.GetProperty("primaryEmail").GetString(), detail.GetProperty("primaryEmail").GetString());
        Assert.Equal(invitedOperator ? "Invited" : "Active", detail.GetProperty("state").GetString());
        Assert.Equal(invitedOperator ? "Operator" : "Person", detail.GetProperty("kind").GetString());
        if (invitedOperator) Assert.Equal(JsonValueKind.Null, detail.GetProperty("displayName").ValueKind);
        var methods = detail.GetProperty("loginMethods");
        Assert.Equal(["Email", "Google"], methods.EnumerateArray().Select(method => method.GetProperty("type").GetString()));
        Assert.DoesNotContain(email, methods.GetRawText());
        Assert.DoesNotContain(googleEmail, methods.GetRawText());
        Assert.All(methods.EnumerateArray(), method =>
        {
            Assert.False(method.TryGetProperty("value", out _));
            Assert.Contains("•••", method.GetProperty("maskedValue").GetString());
        });
        Assert.False(detail.TryGetProperty("version", out _));
    }

    [Fact]
    public async Task Account_detail_returns_null_without_a_primary_email_and_for_a_deleted_account_with_a_legacy_copy()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var viewer = await AccountJourney.RegisterAsync(factory, client, Ct);
        await PlatformOperators.GrantAsync(factory.Services, viewer.UserId, [PlatformPermissions.Accounts.Read], Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var prefix = Guid.NewGuid().ToString("N");
        var withoutPrimary = User("Sin principal " + prefix);
        var deleted = User("Eliminada histórica " + prefix);
        var legacyEmail = "historico-" + prefix + "@example.test";
        deleted.Email = legacyEmail;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            db.Users.AddRange(withoutPrimary, deleted);
            db.Entry(deleted).Property(user => user.Status).CurrentValue = UserStatus.Deleted;
            db.LoginMethods.Add(Method(withoutPrimary.Id, "secundario-" + prefix + "@example.test", false, nowUtc, null));
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);

        var row = Row(await GetAsync(client, "paged?search=" + prefix), withoutPrimary.Id);
        var detail = await GetAsync(client, withoutPrimary.Id.ToString("D"));
        Assert.Equal(JsonValueKind.Null, row.GetProperty("primaryEmail").ValueKind);
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("primaryEmail").ValueKind);
        Assert.False(Assert.Single(detail.GetProperty("loginMethods").EnumerateArray()).GetProperty("isPrimary").GetBoolean());
        var deletedDetail = await GetAsync(client, deleted.Id.ToString("D"));
        Assert.Equal("Deleted", deletedDetail.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, deletedDetail.GetProperty("primaryEmail").ValueKind);
        Assert.DoesNotContain(legacyEmail, deletedDetail.GetRawText());
        Assert.Equal(legacyEmail, await db.Users.Where(user => user.Id == deleted.Id).Select(user => user.Email).SingleAsync(Ct));
    }

    [Fact]
    public async Task Account_detail_uses_only_effective_state_events_of_its_target_with_deterministic_ties()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var viewer = await AccountJourney.RegisterAsync(factory, client, Ct);
        await PlatformOperators.GrantAsync(factory.Services, viewer.UserId, [PlatformPermissions.Accounts.Read], Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        nowUtc = nowUtc.AddTicks(-(nowUtc.Ticks % TimeSpan.TicksPerMicrosecond));
        var account = User("Suspendida con evidencia");
        Assert.True(account.Suspend().IsSuccess);
        var other = User("Otra cuenta");
        var effective = new SecurityEventChanges();
        effective.AddEnum("status", UserStatus.Active, UserStatus.Suspended);
        var noAfter = new SecurityEventChanges();
        noAfter.AddEnum<UserStatus>("status", UserStatus.Active, null);
        var tied = new[]
        {
            SecurityEvent.ForAccountAdministration(SecurityEventType.AccountSuspended, viewer.UserId, account.Id,
                "Primera revisión", nowUtc.AddDays(-2)).WithEvidence(effective, null),
            SecurityEvent.ForAccountAdministration(SecurityEventType.AccountSuspended, viewer.UserId, account.Id,
                "Segunda revisión", nowUtc.AddDays(-2)).WithEvidence(effective, null),
        };
        var sameStatus = SecurityEvent.ForAccountAdministration(SecurityEventType.AccountReactivated,
            viewer.UserId, account.Id, "Replay sin transición", nowUtc);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            db.Users.AddRange(account, other);
            db.SecurityEvents.AddRange(tied);
            db.SecurityEvents.AddRange(sameStatus,
                SecurityEvent.ForAccountAdministration(SecurityEventType.AccountReactivated, viewer.UserId,
                    account.Id, "Histórica sin evidencia", nowUtc),
                SecurityEvent.ForAccountAdministration(SecurityEventType.AccountSuspended, viewer.UserId,
                    account.Id, "Sin estado posterior", nowUtc).WithEvidence(noAfter, null),
                SecurityEvent.ForAccountAdministration(SecurityEventType.AccountSessionsRevoked, viewer.UserId,
                    account.Id, "Sesiones", nowUtc).WithEvidence(effective, null),
                SecurityEvent.ForAccount(SecurityEventType.LoginMethodChanged, account.Id, nowUtc).WithEvidence(effective, null),
                SecurityEvent.ForPlatformOperatorGrant("Permisos", nowUtc, account.Id).WithEvidence(effective, null),
                SecurityEvent.ForAccount(SecurityEventType.PlatformTenantEntered, account.Id, nowUtc).WithEvidence(effective, null),
                SecurityEvent.ForAccountAdministration(SecurityEventType.AccountSuspended, viewer.UserId,
                    other.Id, "AJENA", nowUtc).WithEvidence(effective, null));
            // Una fila histórica que conserva valores iguales también debe quedar fuera, aunque tenga after.
            db.Entry(sameStatus).Property(item => item.Changes).CurrentValue =
                """{"status":{"before":"Suspended","after":"Suspended"}}""";
            db.LoginAudits.Add(LoginAudit.Success(account.Id, LoginAuditMethod.Code, nowUtc));
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);

        var expected = tied.OrderByDescending(item => item.Id).First();
        var count = await db.SecurityEvents.CountAsync(Ct);
        var detail = await GetAsync(client, account.Id.ToString("D"));
        var last = detail.GetProperty("lastStateChange");
        Assert.Equal(expected.OccurredAtUtc, last.GetProperty("atUtc").GetDateTime());
        Assert.EndsWith("Z", last.GetProperty("atUtc").GetString());
        Assert.Equal(expected.Reason, last.GetProperty("reason").GetString());
        Assert.Equal("Suspendió la cuenta", last.GetProperty("action").GetString());
        Assert.False(detail.TryGetProperty("version", out _));
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("deletedAtUtc").ValueKind);
        Assert.Equal(count, await db.SecurityEvents.CountAsync(Ct));
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US");
        var translated = await GetAsync(client, account.Id.ToString("D"));
        Assert.Equal("Suspended the account", translated.GetProperty("lastStateChange").GetProperty("action").GetString());
    }

    [Fact]
    public async Task Account_detail_keeps_unknown_deletion_dates_and_a_recorded_reason_null()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var viewer = await AccountJourney.RegisterAsync(factory, client, Ct);
        await PlatformOperators.GrantAsync(factory.Services, viewer.UserId, [PlatformPermissions.Accounts.Read], Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        nowUtc = nowUtc.AddTicks(-(nowUtc.Ticks % TimeSpan.TicksPerMicrosecond));
        var legacy = User("Baja histórica sin fecha");
        var cancelled = User("Baja cancelada sin motivo registrado");
        var changes = new SecurityEventChanges();
        changes.AddEnum("status", UserStatus.PendingDeletion, UserStatus.Active);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            db.Users.AddRange(legacy, cancelled);
            db.Entry(legacy).Property(user => user.Status).CurrentValue = UserStatus.Deleted;
            db.SecurityEvents.Add(SecurityEvent.ForAccount(SecurityEventType.AccountDeleted, legacy.Id, nowUtc));
            db.SecurityEvents.Add(SecurityEvent.ForAccount(SecurityEventType.AccountDeletionCancelled,
                cancelled.Id, nowUtc).WithEvidence(changes, null));
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);

        var deleted = await GetAsync(client, legacy.Id.ToString("D"));
        Assert.Equal("Deleted", deleted.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, deleted.GetProperty("lastStateChange").ValueKind);
        Assert.Equal(JsonValueKind.Null, deleted.GetProperty("createdAtUtc").ValueKind);
        Assert.Equal(JsonValueKind.Null, deleted.GetProperty("deletedAtUtc").ValueKind);
        var last = (await GetAsync(client, cancelled.Id.ToString("D"))).GetProperty("lastStateChange");
        Assert.Equal(nowUtc, last.GetProperty("atUtc").GetDateTime());
        Assert.Equal("Canceló la baja de la cuenta", last.GetProperty("action").GetString());
        Assert.Equal(JsonValueKind.Null, last.GetProperty("reason").ValueKind);
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
        Assert.True(deleted.CompleteDeletion(now).IsSuccess);
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
