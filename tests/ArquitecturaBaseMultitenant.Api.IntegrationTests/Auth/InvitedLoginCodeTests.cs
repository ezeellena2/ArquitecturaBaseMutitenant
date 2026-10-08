using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Application.Services.Invitations;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

/// <summary>ADR 0047: el código prueba al invitado sin cuenta y acepta, sin consentir documentos por él.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class InvitedLoginCodeTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
        { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

    [Theory]
    [InlineData("business")]
    [InlineData("consumer")]
    public async Task Valid_invitation_creates_account_and_accepts_in_the_requested_door_without_legal_consent(string access)
    {
        var issued = await IssueAsync();
        using var client = CreateClient();
        var code = await RequestAsync(client, issued.Email);
        using var verified = await VerifyAsync(client, issued.Email, code, access);
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        await AssertAcceptedAsync(issued);
        await AccountJourney.AuthorizeAsync(client, access, Ct);
        using var home = await client.GetAsync("/api/home", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, home.StatusCode);
        var problem = await home.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("Legal.AcceptanceRequired", problem.GetProperty("code").GetString());
        await using var check = factory.Services.CreateAsyncScope();
        var userId = await check.ServiceProvider.GetRequiredService<IUserLookup>()
            .FindVerifiedUserIdAsync(LoginMethodType.Email, issued.Email.Value, Ct);
        Assert.NotNull(userId);
        var profiles = await check.ServiceProvider.GetRequiredService<IUserTenantAccessReader>().ListForUserAsync(userId.Value, Ct);
        Assert.Contains(profiles, row => row.TenantId == issued.TenantId && row.MemberStatus == MemberStatus.Active);
        Assert.Equal(access == "consumer" ? 1 : 0, profiles.Count(row => row.Kind == TenantKind.Personal));
        Assert.Empty(await check.ServiceProvider.GetRequiredService<ApplicationDbContext>().LegalAcceptances
            .Where(row => row.UserId == userId).ToArrayAsync(Ct));
        using var replay = await VerifyAsync(client, issued.Email, code, access);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("revoked")]
    public async Task Expired_or_revoked_invitation_does_not_issue_a_code_or_create_an_account(string state)
    {
        var issued = await IssueAsync();
        await ChangeInvitationAsync(issued, state);
        using var client = CreateClient();
        using var response = await AccountJourney.PostAsync(client, "/api/auth/login-code", new { email = issued.Email.Value }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(LoginCodeErrors.AccountNotFoundCode, problem.GetProperty("code").GetString());
        await AssertNoAccountAsync(issued.Email);
        await using var check = factory.Services.CreateAsyncScope();
        Assert.False(await check.ServiceProvider.GetRequiredService<ApplicationDbContext>().LoginCodes
            .AnyAsync(row => row.Destination == issued.Email.Value, Ct));
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("revoked")]
    public async Task Invitation_that_changes_after_request_cannot_create_an_account(string state)
    {
        var issued = await IssueAsync();
        using var client = CreateClient();
        var code = await RequestAsync(client, issued.Email);
        await ChangeInvitationAsync(issued, state);
        using var verified = await VerifyAsync(client, issued.Email, code, "business");
        Assert.Equal(HttpStatusCode.BadRequest, verified.StatusCode);
        Assert.False(verified.Headers.Contains("Set-Cookie"));
        await AssertNoAccountAsync(issued.Email);
    }

    [Fact]
    public async Task Simultaneous_code_verifications_create_one_account_and_accept_once()
    {
        var issued = await IssueAsync();
        using var first = CreateClient();
        using var second = CreateClient();
        var code = await RequestAsync(first, issued.Email);
        var responses = await Task.WhenAll(VerifyAsync(first, issued.Email, code, "business"),
            VerifyAsync(second, issued.Email, code, "consumer"));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.BadRequest);
            await AssertAcceptedAsync(issued);
            await using var scope = factory.Services.CreateAsyncScope();
            Assert.Single(await scope.ServiceProvider.GetRequiredService<IUserLookup>().FindVerifiedUsersByEmailAsync(issued.Email, Ct));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task One_code_accepts_all_live_invitations_and_leaves_other_destinations_pending()
    {
        var first = await IssueAsync(name: "A · primera organización");
        var second = await IssueAsync(first.Email, "Z · segunda organización");
        var foreign = await IssueAsync(name: "Otra persona");
        using var client = CreateClient();
        var code = await RequestAsync(client, first.Email);
        using var verified = await VerifyAsync(client, first.Email, code, "business");
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        await AssertAcceptedAsync(first);
        await AssertAcceptedAsync(second);
        await AssertStatusAsync(foreign, InvitationStatus.Pending, MemberStatus.Invited);
        await using var scope = factory.Services.CreateAsyncScope();
        var userId = Assert.Single(await scope.ServiceProvider.GetRequiredService<IUserLookup>().FindVerifiedUsersByEmailAsync(first.Email, Ct));
        var selected = await scope.ServiceProvider.GetRequiredService<IConnectService>().GetActiveUserAsync(userId, Access.Business, null, Ct);
        Assert.True(selected.IsSuccess);
        Assert.Equal(first.TenantId, selected.Value.TenantId);
        var profiles = await scope.ServiceProvider.GetRequiredService<IUserTenantAccessReader>().ListForUserAsync(userId, Ct);
        Assert.Equal(2, profiles.Count(row => row.Kind == TenantKind.Business));
        Assert.DoesNotContain(profiles, row => row.TenantId == foreign.TenantId);
    }

    [Theory]
    [InlineData("business")]
    [InlineData("consumer")]
    public async Task All_owner_invitations_keep_the_role_permissions_and_platform_owners_in_both_organizations(string access)
    {
        var first = await IssueAsync(name: "A · primera Dueña");
        var second = await IssueAsync(first.Email, "Z · segunda Dueña");
        foreach (var issued in new[] { first, second })
            await SetOwnerRoleAsync(issued, assigned: true);
        using var client = CreateClient();
        var code = await RequestAsync(client, first.Email);
        using var verified = await VerifyAsync(client, first.Email, code, access);
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        await using var check = factory.Services.CreateAsyncScope();
        var userId = Assert.Single(await check.ServiceProvider.GetRequiredService<IUserLookup>().FindVerifiedUsersByEmailAsync(first.Email, Ct));
        var profiles = await check.ServiceProvider.GetRequiredService<IUserTenantAccessReader>().ListForUserAsync(userId, Ct);
        foreach (var issued in new[] { first, second })
        {
            await AssertAcceptedAsync(issued);
            await using var organization = factory.Services.CreateAsyncScope();
            organization.ServiceProvider.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(issued.TenantId, TenantKind.Business);
            Assert.Equal(issued.TenantId, organization.ServiceProvider.GetRequiredService<ITenantContext>().TenantId);
            Assert.Contains(Permissions.Users.Read, await organization.ServiceProvider.GetRequiredService<IPermissionService>()
                .GetOrganizationPermissionsAsync(userId, Ct));
            var profile = Assert.Single(profiles, row => row.TenantId == issued.TenantId);
            Assert.Equal(SystemRoles.TenantAdmin, profile.RoleName);
            var details = await check.ServiceProvider.GetRequiredService<IPlatformReader>().FindTenantDetailAsync(issued.TenantId, Ct);
            Assert.Contains(details!.Owners, row => row.Id == userId);
            Assert.Contains(userId, await check.ServiceProvider.GetRequiredService<IPlatformReader>()
                .ListActiveTenantOwnerIdsAsync(issued.TenantId, Ct));
        }
        await SetOwnerRoleAsync(second, assigned: false);
        var withoutOwner = await check.ServiceProvider.GetRequiredService<IUserTenantAccessReader>().ListForUserAsync(userId, Ct);
        Assert.Null(Assert.Single(withoutOwner, row => row.TenantId == second.TenantId).RoleName);
        Assert.DoesNotContain(userId, await check.ServiceProvider.GetRequiredService<IPlatformReader>()
            .ListActiveTenantOwnerIdsAsync(second.TenantId, Ct));
        await SetOwnerRoleAsync(second, assigned: true);
        var restored = await check.ServiceProvider.GetRequiredService<IUserTenantAccessReader>().ListForUserAsync(userId, Ct);
        Assert.Equal(SystemRoles.TenantAdmin, Assert.Single(restored, row => row.TenantId == second.TenantId).RoleName);
        Assert.Contains(userId, await check.ServiceProvider.GetRequiredService<IPlatformReader>()
            .ListActiveTenantOwnerIdsAsync(second.TenantId, Ct));
    }

    [Fact]
    public async Task Migration_repairs_existing_owner_accesses_and_preserves_unbound_owner_reservations()
    {
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        var journey = new InvitedLoginCodeTests(isolated);
        using var client = journey.CreateClient();
        await using var migrationScope = isolated.Services.CreateAsyncScope();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(isolated.AdminConnectionString);
        options.UseOpenIddict<Guid>();
        await using var ownerContext = new ApplicationDbContext(options.Options,
            migrationScope.ServiceProvider.GetRequiredService<ITenantContext>());
        var migrator = ownerContext.Database.GetService<IMigrator>();
        var first = await journey.IssueAsync(name: "A · Dueña previa");
        var second = await journey.IssueAsync(first.Email, "Z · Dueña previa");
        var pending = await journey.IssueAsync(name: "Dueña reservada");
        foreach (var issued in new[] { first, second, pending })
            await journey.SetOwnerRoleAsync(issued, assigned: true);
        var code = await journey.RequestAsync(client, first.Email);
        using var verified = await VerifyAsync(client, first.Email, code, "business");
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        var lookup = migrationScope.ServiceProvider.GetRequiredService<IUserLookup>();
        var userId = Assert.Single(await lookup.FindVerifiedUsersByEmailAsync(first.Email, Ct));
        var accesses = migrationScope.ServiceProvider.GetRequiredService<IUserTenantAccessReader>();
        // Create through the current API before downgrading; its EF model cannot write the historical outbox.
        await migrator.MigrateAsync("20261007123554_LoginInvitations", Ct);
        // Reproduce the old projection missing the second owner's role, without writing through a newer EF model.
        await ownerContext.Database.ExecuteSqlInterpolatedAsync($"UPDATE identity.\"UserTenantAccesses\" SET \"RoleName\" = NULL WHERE \"UserId\" = {userId} AND \"TenantId\" = {second.TenantId}", Ct);
        var before = await accesses.ListForUserAsync(userId, Ct);
        Assert.Null(Assert.Single(before, row => row.TenantId == second.TenantId).RoleName);
        await journey.AssertNoAccountAsync(pending.Email);

        await migrator.MigrateAsync(cancellationToken: Ct);

        var repaired = await accesses.ListForUserAsync(userId, Ct);
        foreach (var issued in new[] { first, second })
        {
            Assert.Equal(SystemRoles.TenantAdmin, Assert.Single(repaired, row => row.TenantId == issued.TenantId).RoleName);
            Assert.Contains(userId, await migrationScope.ServiceProvider.GetRequiredService<IPlatformReader>()
                .ListActiveTenantOwnerIdsAsync(issued.TenantId, Ct));
        }
        var pendingCode = await journey.RequestAsync(client, pending.Email);
        using var pendingVerified = await VerifyAsync(client, pending.Email, pendingCode, "business");
        Assert.Equal(HttpStatusCode.OK, pendingVerified.StatusCode);
        await journey.AssertAcceptedAsync(pending);
        var pendingUserId = Assert.Single(await lookup.FindVerifiedUsersByEmailAsync(pending.Email, Ct));
        Assert.Equal(SystemRoles.TenantAdmin, Assert.Single(await accesses.ListForUserAsync(pendingUserId, Ct),
            row => row.TenantId == pending.TenantId).RoleName);
    }

    [Fact]
    public async Task Runtime_cannot_read_write_or_refresh_the_internal_member_role_projection()
    {
        await using var connection = await RuntimeRoleConnection.OpenAsync(factory.RuntimeConnectionString, null, Ct);
        await using var privileges = new NpgsqlCommand("""
            SELECT has_table_privilege(current_user, 'identity."MemberAccessRoles"', 'SELECT,INSERT,UPDATE,DELETE'),
                   has_function_privilege(current_user, 'identity.refresh_user_tenant_access_role(uuid, uuid)', 'EXECUTE'),
                   has_function_privilege(current_user, 'identity.sync_user_tenant_access()', 'EXECUTE'),
                   has_function_privilege(current_user, 'identity.sync_role_assignment_access()', 'EXECUTE'),
                   has_function_privilege(current_user, 'identity.sync_role_name_access()', 'EXECUTE')
            """, connection);
        await using (var reader = await privileges.ExecuteReaderAsync(Ct))
        {
            Assert.True(await reader.ReadAsync(Ct));
            for (var column = 0; column < reader.FieldCount; column++) Assert.False(reader.GetBoolean(column));
        }
        await using var read = new NpgsqlCommand("SELECT count(*) FROM identity.\"MemberAccessRoles\"", connection);
        var denied = await Assert.ThrowsAsync<PostgresException>(() => read.ExecuteScalarAsync(Ct));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
    }

    private async Task SetOwnerRoleAsync(Issued issued, bool assigned)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(issued.TenantId);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var memberId = (await services.GetRequiredService<IInvitationReader>().FindByIdAsync(issued.Id, ct))!.MemberId;
            var role = await services.GetRequiredService<ApplicationDbContext>().Roles
                .SingleAsync(row => row.Name == SystemRoles.TenantAdmin, ct);
            var assignments = services.GetRequiredService<IRoleAssignmentRepository>();
            if (assigned) assignments.Add(RoleAssignment.Assign(memberId, role.Id, role.Scope, role.CompanyId, null).Value);
            else assignments.Remove(Assert.Single(await assignments.ListForMemberAsync(memberId, ct), row => row.RoleId == role.Id));
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("revoked")]
    public async Task A_live_invitation_accepts_without_consuming_an_expired_or_revoked_one(string state)
    {
        var first = await IssueAsync();
        var second = await IssueAsync(first.Email);
        using var client = CreateClient();
        var code = await RequestAsync(client, first.Email);
        await ChangeInvitationAsync(second, state);
        using var verified = await VerifyAsync(client, first.Email, code, "consumer");
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        await AssertAcceptedAsync(first);
        await AssertStatusAsync(second, state == "revoked" ? InvitationStatus.Revoked : InvitationStatus.Pending, MemberStatus.Invited);
    }

    [Fact]
    public async Task Suspended_organization_prevents_partial_acceptance_and_account_creation()
    {
        var first = await IssueAsync();
        var second = await IssueAsync(first.Email);
        await using (var connection = await RuntimeRoleConnection.OpenAsync(factory.RuntimeConnectionString, null, Ct))
        {
            await using var command = new NpgsqlCommand("UPDATE platform.\"Tenants\" SET \"Status\" = 'Suspended' WHERE \"Id\" = @id", connection);
            command.Parameters.AddWithValue("id", second.TenantId);
            Assert.Equal(1, await command.ExecuteNonQueryAsync(Ct));
        }
        using var client = CreateClient();
        var code = await RequestAsync(client, first.Email);
        using var verified = await VerifyAsync(client, first.Email, code, "business");
        Assert.Equal(HttpStatusCode.Forbidden, verified.StatusCode);
        await AssertNoAccountAsync(first.Email);
        await AssertStatusAsync(first, InvitationStatus.Pending, MemberStatus.Invited);
        await AssertStatusAsync(second, InvitationStatus.Pending, MemberStatus.Invited);
    }

    [Fact]
    public async Task Existing_account_accepts_all_invitations_and_keeps_its_last_used_active_organization()
    {
        using var registered = CreateClient();
        var account = await AccountJourney.RegisterAsync(factory, registered, Ct);
        var existing = await OrganizationJourney.StageAsync(factory, account, "Organización ya usada", Ct);
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                await scope.ServiceProvider.GetRequiredService<IUserRepository>().RememberBusinessTenantAsync(account.UserId, existing, ct);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        var first = await IssueAsync(account.Email);
        var second = await IssueAsync(account.Email);
        using var client = CreateClient();
        // El código de registro ya consumido pertenece a la preparación, anterior a la ventana de ingreso.
        await using (var connection = await RuntimeRoleConnection.OpenAsync(factory.RuntimeConnectionString, null, Ct))
        {
            await using var command = new NpgsqlCommand("UPDATE identity.\"LoginCodes\" SET \"CreatedAtUtc\" = @earlier WHERE \"Destination\" = @email", connection);
            command.Parameters.AddWithValue("earlier", factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime.AddHours(-1));
            command.Parameters.AddWithValue("email", account.Email.Value);
            await command.ExecuteNonQueryAsync(Ct);
        }
        var code = await RequestAsync(client, first.Email);
        using var verified = await VerifyAsync(client, first.Email, code, "business");
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        await AssertAcceptedAsync(first);
        await AssertAcceptedAsync(second);
        await using var check = factory.Services.CreateAsyncScope();
        Assert.Equal(account.UserId, Assert.Single(await check.ServiceProvider.GetRequiredService<IUserLookup>().FindVerifiedUsersByEmailAsync(account.Email, Ct)));
        var selected = await check.ServiceProvider.GetRequiredService<IConnectService>().GetActiveUserAsync(account.UserId, Access.Business, null, Ct);
        Assert.Equal(existing, selected.Value.TenantId);
    }

    [Fact]
    public async Task Scoped_acceptance_flags_do_not_expose_unrelated_members_settings_companies_roles_or_audit()
    {
        var own = await IssueAsync();
        var foreign = await IssueAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(own.TenantId);
        var row = await scope.ServiceProvider.GetRequiredService<IInvitationReader>().FindByIdAsync(own.Id, Ct);
        await using var connection = await RuntimeRoleConnection.OpenAsync(factory.RuntimeConnectionString, null, Ct);
        await using var flags = new NpgsqlCommand("""
            SELECT set_config('app.login_invitation_destination', @email, false),
                   set_config('app.login_invitation_keys', @key, false),
                   set_config('app.login_invitation_member_keys', @member, false)
            """, connection);
        flags.Parameters.AddWithValue("email", own.Email.Value);
        flags.Parameters.AddWithValue("key", $"{own.TenantId:D}:{own.Id:D}");
        flags.Parameters.AddWithValue("member", $"{own.TenantId:D}:{row!.MemberId:D}");
        await flags.ExecuteNonQueryAsync(Ct);
        foreach (var table in new[] { "Members", "Invitations", "TenantSettings", "Companies", "Roles", "RoleAssignments", "AuditEntries" })
        {
            await using var command = new NpgsqlCommand($"SELECT count(*) FROM tenant.\"{table}\"", connection);
            Assert.Equal(table is "Members" or "Invitations" ? 1L : 0L, (long)(await command.ExecuteScalarAsync(Ct))!);
        }
        await using var unrelated = new NpgsqlCommand("SELECT count(*) FROM tenant.\"Invitations\" WHERE \"TenantId\" = @id", connection);
        unrelated.Parameters.AddWithValue("id", foreign.TenantId);
        Assert.Equal(0L, (long)(await unrelated.ExecuteScalarAsync(Ct))!);
        await using var mismatched = new NpgsqlCommand("SELECT set_config('app.login_invitation_keys', @key, false)", connection);
        mismatched.Parameters.AddWithValue("key", $"{foreign.TenantId:D}:{foreign.Id:D}");
        await mismatched.ExecuteNonQueryAsync(Ct);
        foreach (var table in new[] { "Invitations", "Members" })
        {
            await using var command = new NpgsqlCommand($"SELECT count(*) FROM tenant.\"{table}\"", connection);
            Assert.Equal(0L, (long)(await command.ExecuteScalarAsync(Ct))!);
        }
        await using var grants = new NpgsqlCommand("SELECT has_table_privilege(current_user, 'identity.\"LoginInvitations\"', 'SELECT'), has_table_privilege(current_user, 'identity.\"LoginInvitations\"', 'INSERT,UPDATE,DELETE')", connection);
        await using var permissions = await grants.ExecuteReaderAsync(Ct);
        Assert.True(await permissions.ReadAsync(Ct));
        Assert.True(permissions.GetBoolean(0));
        Assert.False(permissions.GetBoolean(1));
    }

    private async Task<string> RequestAsync(HttpClient client, Email email)
    {
        using var requested = await AccountJourney.PostAsync(client, "/api/auth/login-code", new { email = email.Value }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        return await PickupCodeReader.ReadAsync(factory.Services, email.Value, Ct);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Managed_email_is_rehabilitated_only_by_its_own_organization_invitation(bool sameOrganization)
    {
        var managed = await CreateManagedAccountAsync();
        using var client = CreateClient();
        var code = await RequestAsync(client, managed.Email);
        await RemoveManagedMemberAsync(managed);
        var invitation = sameOrganization ? await IssueInAsync(managed) : await IssueAsync(managed.Email);
        using var verified = await VerifyAsync(client, managed.Email, code, "consumer");
        Assert.Equal(sameOrganization ? HttpStatusCode.OK : HttpStatusCode.BadRequest, verified.StatusCode);
        await AssertStatusAsync(invitation, sameOrganization ? InvitationStatus.Accepted : InvitationStatus.Pending,
            sameOrganization ? MemberStatus.Active : MemberStatus.Invited);
        if (!sameOrganization) Assert.False(verified.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Reinvited_managed_email_can_request_a_code_from_its_own_organization()
    {
        var managed = await CreateManagedAccountAsync();
        await RemoveManagedMemberAsync(managed);
        var invitation = await IssueInAsync(managed);
        using var client = CreateClient();
        var code = await RequestAsync(client, managed.Email);
        using var verified = await VerifyAsync(client, managed.Email, code, "business");
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        await AssertAcceptedAsync(invitation);
    }

    [Fact]
    public async Task Another_organization_invitation_does_not_send_a_code_to_an_unavailable_managed_email()
    {
        var managed = await CreateManagedAccountAsync();
        await RemoveManagedMemberAsync(managed);
        var invitation = await IssueAsync(managed.Email);
        using var client = CreateClient();
        using var requested = await AccountJourney.PostAsync(client, "/api/auth/login-code", new { email = managed.Email.Value }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var code = Assert.Single(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LoginCodes
            .Where(row => row.Destination == managed.Email.Value).ToArrayAsync(Ct));
        Assert.Null(code.SentAtUtc);
        await AssertStatusAsync(invitation, InvitationStatus.Pending, MemberStatus.Invited);
    }

    [Fact]
    public async Task Verified_Google_contact_accepts_an_invitation_by_email_code_without_another_account()
    {
        var existing = await CreateManagedAccountAsync(googleContact: true, managed: false);
        var invitation = await IssueAsync(existing.Email);
        using var client = CreateClient();
        var code = await RequestAsync(client, existing.Email);
        using var verified = await VerifyAsync(client, existing.Email, code, "consumer");
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        await AssertAcceptedAsync(invitation);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(existing.UserId, Assert.Single(await scope.ServiceProvider.GetRequiredService<IUserLookup>()
            .FindVerifiedUsersByEmailAsync(existing.Email, Ct)));
    }

    private async Task<Managed> CreateManagedAccountAsync(bool googleContact = false, bool managed = true)
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        var email = Email.Create($"managed-invited-{Guid.NewGuid():N}@example.test").Value;
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var account = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var user = await services.GetRequiredService<IUserRepository>().CreateAsync("Cuenta administrada", "es-AR",
                "America/Argentina/Buenos_Aires", ct);
            var method = googleContact ? LoginMethod.CreateGoogle(user.Id, "a9-google-" + Guid.NewGuid().ToString("N"), email)
                : LoginMethod.CreateEmail(user.Id, email);
            method.Verify(services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
            Assert.True(method.MakePrimary().IsSuccess);
            services.GetRequiredService<ILoginMethodRepository>().Add(method);
            await services.GetRequiredService<IUserRepository>().SetPrimaryEmailAsync(user.Id, email, ct);
            return Result.Success(user.Id);
        }, CommitPolicy.OnSuccess, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, new AccountJourney(account.Value, email), "Organización administradora", Ct);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var method = Assert.Single(await services.GetRequiredService<ILoginMethodRepository>().ListByUserIdAsync(account.Value, ct));
            if (managed) method.AssignManagedByOrganization(tenantId);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        return new Managed(account.Value, email, tenantId);
    }

    private async Task RemoveManagedMemberAsync(Managed managed)
    {
        await using var connection = await RuntimeRoleConnection.OpenAsync(factory.RuntimeConnectionString, managed.TenantId, Ct);
        await using var command = new NpgsqlCommand("UPDATE tenant.\"Members\" SET \"Status\" = 'Removed' WHERE \"UserId\" = @user", connection);
        command.Parameters.AddWithValue("user", managed.UserId);
        Assert.Equal(1, await command.ExecuteNonQueryAsync(Ct));
    }

    private async Task<Issued> IssueInAsync(Managed managed)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(managed.TenantId);
        var issued = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
            services.GetRequiredService<InvitationIssuer>().IssueAsync(new IssueInvitationRequest(managed.Email,
                managed.UserId, InvitationChannel.Email), ct), CommitPolicy.OnSuccess, Ct);
        Assert.True(issued.IsSuccess);
        return new Issued(managed.TenantId, issued.Value, managed.Email);
    }

    private sealed record Managed(Guid UserId, Email Email, Guid TenantId)
    {
        public override string ToString() => "Managed [redacted]";
    }

    private static Task<HttpResponseMessage> VerifyAsync(HttpClient client, Email email, string code, string access) =>
        AccountJourney.PostAsync(client, "/api/auth/login-code/verify", new
        {
            email = email.Value, code,
            returnUrl = "/connect/authorize?client_id=web&access=" + access,
        }, Ct);

    private async Task AssertNoAccountAsync(Email email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<IUserLookup>().FindVerifiedUsersByEmailAsync(email, Ct));
    }

    private async Task AssertAcceptedAsync(Issued issued)
        => await AssertStatusAsync(issued, InvitationStatus.Accepted, MemberStatus.Active);

    private async Task AssertStatusAsync(Issued issued, InvitationStatus status, MemberStatus memberStatus)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(issued.TenantId);
        var row = await scope.ServiceProvider.GetRequiredService<IInvitationReader>().FindByIdAsync(issued.Id, Ct);
        Assert.Equal(status, row!.Status);
        Assert.Equal(memberStatus, row.MemberStatus);
        if (status == InvitationStatus.Accepted) Assert.NotNull(row.AcceptedByUserId);
    }

    private async Task ChangeInvitationAsync(Issued issued, string state)
    {
        await using var connection = await RuntimeRoleConnection.OpenAsync(factory.RuntimeConnectionString, issued.TenantId, Ct);
        var sql = state == "expired"
            ? "UPDATE tenant.\"Invitations\" SET \"ExpiresAtUtc\" = @now WHERE \"Id\" = @id"
            : "UPDATE tenant.\"Invitations\" SET \"Status\" = 'Revoked' WHERE \"Id\" = @id";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", issued.Id);
        command.Parameters.AddWithValue("now", factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime.AddSeconds(-1));
        Assert.Equal(1, await command.ExecuteNonQueryAsync(Ct));
    }

    private async Task<Issued> IssueAsync(Email? destination = null, string name = "Organización invitante")
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        var email = destination ?? Email.Create($"invited-login-{Guid.NewGuid():N}@example.test").Value;
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var uow = services.GetRequiredService<IUnitOfWork>();
        var owner = await uow.ExecuteInTransactionAsync(async ct =>
            Result.Success((await services.GetRequiredService<IUserRepository>().CreateAsync("Invitante", "es-AR",
                "America/Argentina/Buenos_Aires", ct)).Id), CommitPolicy.OnSuccess, Ct);
        var organization = Tenant.CreateBusiness(name, requiresApproval: false);
        Assert.True(organization.Activate().IsSuccess);
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(organization.Id);
        await uow.ExecuteInTransactionAsync(_ =>
        {
            services.GetRequiredService<TenantSpaceProvisioner>().Stage(organization,
                TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"), [owner.Value]);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        var issued = await uow.ExecuteInTransactionAsync(ct => services.GetRequiredService<InvitationIssuer>()
            .IssueAsync(new IssueInvitationRequest(email, owner.Value, InvitationChannel.Email), ct), CommitPolicy.OnSuccess, Ct);
        Assert.True(issued.IsSuccess);
        return new Issued(organization.Id, issued.Value, email);
    }

    private sealed record Issued(Guid TenantId, Guid Id, Email Email)
    {
        public override string ToString() => "Issued [redacted]";
    }
}
