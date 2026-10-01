using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Services.Invitations;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

/// <summary>Prueba invitaciones y membresías sin cuenta sobre PostgreSQL aislado, filtros EF y RLS reales.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class InvitationsTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Preview_uses_real_private_rows_and_a_protected_http_only_continuation_without_opening_a_session()
    {
        using var client = factory.CreateClient();
        var (tenantId, inviterId) = await CreateOrganizationAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var tokens = services.GetRequiredService<IInvitationTokenProtector>();
        var secret = tokens.GenerateSecret();
        var member = Member.Invite();
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var invitation = Invitation.Issue(member.Id, inviterId, Email.Create("preview-recipient@example.test").Value,
            InvitationChannel.Email, tokens.Hash(secret), nowUtc, TimeSpan.FromDays(7));
        using (services.GetRequiredService<ITenantScope>().Enter(tenantId))
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
            {
                services.GetRequiredService<IMemberRepository>().Add(member);
                services.GetRequiredService<IInvitationRepository>().Add(invitation);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        var token = tokens.Protect(new InvitationTokenData(tenantId, invitation.Id, secret));
        var accessor = services.GetRequiredService<IHttpContextAccessor>();
        try
        {
            var initial = new DefaultHttpContext { RequestServices = services };
            initial.Request.Scheme = "https";
            accessor.HttpContext = initial;
            var preview = await services.GetRequiredService<IInvitationService>()
                .PreviewAsync(new PreviewInvitationRequest(token), Ct);
            Assert.Equal(InvitationPreviewState.NoAccount, preview.Value.State);
            Assert.Equal(invitation.Destination, preview.Value.Email);
            var cookie = Assert.Single(initial.Response.Headers.SetCookie);
            Assert.NotNull(cookie);
            Assert.Contains("__Host-MtInvitation=", cookie);
            Assert.Contains("secure", cookie);
            Assert.Contains("httponly", cookie);
            Assert.Contains("samesite=lax", cookie);
            Assert.False(cookie.Contains(token, StringComparison.Ordinal));
            var reload = new DefaultHttpContext { RequestServices = services };
            reload.Request.Scheme = "https";
            reload.Request.Headers.Cookie = cookie.Split(';')[0];
            accessor.HttpContext = reload;
            var repeated = await services.GetRequiredService<IInvitationService>()
                .PreviewAsync(new PreviewInvitationRequest(null), Ct);
            Assert.Equal(preview.Value, repeated.Value);
            using (services.GetRequiredService<ITenantScope>().Enter(tenantId))
                Assert.Equal(InvitationStatus.Pending, (await services.GetRequiredService<IInvitationReader>()
                    .FindByIdAsync(invitation.Id, Ct))!.Status);
        }
        finally { accessor.HttpContext = null; }
    }

    [Theory]
    [InlineData("none")]
    [InlineData("email")]
    [InlineData("google")]
    public async Task Issuer_enqueues_a_real_encrypted_email_without_creating_an_identity(string existingAccount)
    {
        using var client = factory.CreateClient();
        var (tenantId, inviterId) = await CreateOrganizationAsync();
        var email = Email.Create($"invite-{Guid.NewGuid():N}@example.test").Value;
        Guid? recipientId = null;
        if (existingAccount != "none") recipientId = await CreateRecipientAsync(email, existingAccount == "google");
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var beforeUsers = await context.Users.CountAsync(Ct);
        var beforeOutbox = await context.OutboxMessages.CountAsync(Ct);
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);

        var issued = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(
            ct => services.GetRequiredService<InvitationIssuer>().IssueAsync(
                new IssueInvitationRequest(email, inviterId, InvitationChannel.Email), ct), CommitPolicy.OnSuccess, Ct);

        Assert.True(issued.IsSuccess);
        Assert.Equal(beforeUsers, await context.Users.CountAsync(Ct));
        Assert.Equal(beforeOutbox + 1, await context.OutboxMessages.CountAsync(Ct));
        var invitation = await services.GetRequiredService<IInvitationReader>().FindByIdAsync(issued.Value, Ct);
        Assert.NotNull(invitation);
        Assert.Equal(recipientId, invitation.MemberUserId);
        Assert.Equal(MemberStatus.Invited, invitation.MemberStatus);
        var outbox = await context.OutboxMessages.AsNoTracking().OrderByDescending(row => row.CreatedAtUtc)
            .ThenByDescending(row => row.Id).FirstAsync(Ct);
        var payload = services.GetRequiredService<IPayloadProtector>().Unprotect(outbox.EncryptedPayload);
        var message = JsonSerializer.Deserialize<EmailMessage>(payload);
        Assert.NotNull(message);
        Assert.True(message.To == email.Value);
        var link = Regex.Match(message.TextBody, @"https://[^\s]+/invitacion#(?<token>[A-Za-z0-9_-]+)");
        Assert.True(link.Success);
        var tokens = services.GetRequiredService<IInvitationTokenProtector>();
        var token = tokens.Unprotect(link.Groups["token"].Value);
        Assert.NotNull(token);
        Assert.Equal(tenantId, token.TenantId);
        Assert.Equal(invitation.Id, token.InvitationId);
        Assert.True(invitation.TokenHash == services.GetRequiredService<ISecureTokenGenerator>().Hash(token.Secret));
        Assert.False(outbox.EncryptedPayload.Contains(token.Secret, StringComparison.Ordinal));
        Assert.Null(tokens.Unprotect(link.Groups["token"].Value + "tampered"));
        Assert.Equal(existingAccount == "email", message.TextBody.Contains("te vamos a mandar un código", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Issuer_rejects_an_unregistered_channel_without_leaving_rows_or_outbox()
    {
        using var client = factory.CreateClient();
        var (tenantId, inviterId) = await CreateOrganizationAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var beforeOutbox = await context.OutboxMessages.CountAsync(Ct);
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);

        var issued = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(
            ct => services.GetRequiredService<InvitationIssuer>().IssueAsync(new IssueInvitationRequest(
                Email.Create("unknown-channel@example.test").Value, inviterId, "unknown"), ct), CommitPolicy.OnSuccess, Ct);

        Assert.Equal(InvitationErrors.ChannelUnavailable, issued.Error);
        Assert.Equal(0, await context.Invitations.CountAsync(Ct));
        Assert.Equal(0, await context.Members.CountAsync(Ct));
        Assert.Equal(beforeOutbox, await context.OutboxMessages.CountAsync(Ct));
    }

    [Fact]
    public async Task Rolling_back_the_callers_transaction_discards_invitation_member_and_email()
    {
        using var client = factory.CreateClient();
        var (tenantId, inviterId) = await CreateOrganizationAsync();
        long beforeOutbox;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            beforeOutbox = await services.GetRequiredService<ApplicationDbContext>().OutboxMessages.CountAsync(Ct);
            using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var issued = await services.GetRequiredService<InvitationIssuer>().IssueAsync(new IssueInvitationRequest(
                    Email.Create("rollback-invite@example.test").Value, inviterId, InvitationChannel.Email), ct);
                Assert.True(issued.IsSuccess);
                return Result.Failure(InvitationErrors.Invalid);
            }, CommitPolicy.OnSuccess, Ct);
        }
        await using var check = factory.Services.CreateAsyncScope();
        using var active = check.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
        var context = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(0, await context.Invitations.CountAsync(Ct));
        Assert.Equal(0, await context.Members.CountAsync(Ct));
        Assert.Equal(beforeOutbox, await context.OutboxMessages.CountAsync(Ct));
    }

    [Fact]
    public async Task Issuer_rejects_a_second_pending_invitation_without_duplicating_rows_or_email()
    {
        using var client = factory.CreateClient();
        var (tenantId, inviterId) = await CreateOrganizationAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        var request = new IssueInvitationRequest(Email.Create("duplicate-invite@example.test").Value,
            inviterId, InvitationChannel.Email);
        var uow = services.GetRequiredService<IUnitOfWork>();
        Assert.True((await uow.ExecuteInTransactionAsync(ct =>
            services.GetRequiredService<InvitationIssuer>().IssueAsync(request, ct), CommitPolicy.OnSuccess, Ct)).IsSuccess);
        var context = services.GetRequiredService<ApplicationDbContext>();
        var beforeOutbox = await context.OutboxMessages.CountAsync(Ct);

        var repeated = await uow.ExecuteInTransactionAsync(ct =>
            services.GetRequiredService<InvitationIssuer>().IssueAsync(request, ct), CommitPolicy.OnSuccess, Ct);

        Assert.Equal(InvitationErrors.AlreadyPending, repeated.Error);
        Assert.Equal(1, await context.Invitations.CountAsync(Ct));
        Assert.Equal(1, await context.Members.CountAsync(Ct));
        Assert.Equal(beforeOutbox, await context.OutboxMessages.CountAsync(Ct));
    }

    private async Task<Guid> CreateRecipientAsync(Email email, bool googleOnly = false)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var userId = Guid.Empty;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            userId = (await services.GetRequiredService<IUserRepository>()
                .CreateAsync("Recipient", "es-AR", "America/Argentina/Buenos_Aires", ct)).Id;
            var method = googleOnly ? LoginMethod.CreateGoogle(userId, $"google-{Guid.NewGuid():N}", email)
                : LoginMethod.CreateEmail(userId, email);
            method.Verify(new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc));
            Assert.True(method.MakePrimary().IsSuccess);
            services.GetRequiredService<ILoginMethodRepository>().Add(method);
            if (!googleOnly) await services.GetRequiredService<IUserRepository>().SetPrimaryEmailAsync(userId, email, ct);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        return userId;
    }

    [Fact]
    public async Task Unbound_member_is_private_and_enters_the_access_index_only_after_binding()
    {
        using var client = factory.CreateClient();
        var (tenantId, userId) = await CreateOrganizationAsync();
        var member = Member.Invite();
        var invitation = Invitation.Issue(member.Id, userId, Email.Create("recipient@example.test").Value,
            InvitationChannel.Email, "private-hash", new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
            TimeSpan.FromDays(7));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);
            var saved = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
            {
                services.GetRequiredService<IMemberRepository>().Add(member);
                services.GetRequiredService<IInvitationRepository>().Add(invitation);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
            Assert.True(saved.IsSuccess);
        }

        Assert.Equal(0, await CountAccessesAsync(userId, tenantId));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);
            Assert.NotNull(await services.GetRequiredService<IInvitationReader>().FindByIdAsync(invitation.Id, Ct));
            var saved = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var stored = await services.GetRequiredService<IMemberRepository>().GetByIdAsync(member.Id, ct);
                Assert.NotNull(stored);
                Assert.Null(stored.UserId);
                Assert.True(stored.AssignUser(userId).IsSuccess);
                Assert.True(stored.Activate(new DateTime(2026, 10, 1, 13, 0, 0, DateTimeKind.Utc)).IsSuccess);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
            Assert.True(saved.IsSuccess);
        }

        Assert.Equal(1, await CountAccessesAsync(userId, tenantId));
    }

    [Fact]
    public async Task Another_organization_cannot_read_the_invitation_through_reader_or_runtime_sql()
    {
        using var client = factory.CreateClient();
        var (firstTenant, inviter) = await CreateOrganizationAsync();
        var (otherTenant, _) = await CreateOrganizationAsync();
        var member = Member.Invite();
        var invitation = Invitation.Issue(member.Id, inviter, Email.Create("private@example.test").Value,
            InvitationChannel.Email, "isolation-hash", new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
            TimeSpan.FromDays(7));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(firstTenant);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
            {
                services.GetRequiredService<IMemberRepository>().Add(member);
                services.GetRequiredService<IInvitationRepository>().Add(invitation);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            using var tenantScope = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(otherTenant);
            Assert.Null(await scope.ServiceProvider.GetRequiredService<IInvitationReader>()
                .FindByIdAsync(invitation.Id, Ct));
        }
        await using var connection = await RuntimeRoleConnection.OpenAsync(factory.RuntimeConnectionString, otherTenant, Ct);
        await using var command = new NpgsqlCommand("SELECT COUNT(*) FROM tenant.\"Invitations\" WHERE \"Id\" = @id", connection);
        command.Parameters.AddWithValue("id", invitation.Id);
        Assert.Equal(0L, await command.ExecuteScalarAsync(Ct));
    }

    private async Task<(Guid TenantId, Guid UserId)> CreateOrganizationAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var tenant = Tenant.CreateBusiness("Invitation test", requiresApproval: false);
        Assert.True(tenant.Activate().IsSuccess);
        var userId = Guid.Empty;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            userId = (await services.GetRequiredService<IUserRepository>()
                .CreateAsync("Inviter", "es-AR", "America/Argentina/Buenos_Aires", ct)).Id;
            services.GetRequiredService<ITenantRepository>().Add(tenant);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        return (tenant.Id, userId);
    }

    private async Task<long> CountAccessesAsync(Guid userId, Guid tenantId)
    {
        await using var connection = new NpgsqlConnection(factory.RuntimeConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("""
            SELECT COUNT(*) FROM identity."UserTenantAccesses" WHERE "UserId" = @user_id AND "TenantId" = @tenant_id
            """, connection);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        return (long)(await command.ExecuteScalarAsync(Ct))!;
    }
}
