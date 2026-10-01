using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Application.Services.Invitations;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

/// <summary>Reglas de aceptación con Identity, UoW, outbox y PostgreSQL reales; HTTP/idempotencia se prueban en T08.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class InvitationAcceptanceTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("none")]
    [InlineData("email")]
    [InlineData("google")]
    public async Task Acceptance_binds_one_identity_and_never_provisions_a_personal_space(string kind)
    {
        using var client = factory.CreateClient();
        var issued = await IssueAsync(kind);
        await using var check = factory.Services.CreateAsyncScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var usersBefore = await db.Users.CountAsync(Ct);
        var personalBefore = await db.Tenants.CountAsync(row => row.Kind == TenantKind.Personal, Ct);
        var legalBefore = await db.LegalAcceptances.CountAsync(Ct);

        var accepted = await AcceptAsync(issued, issued.RecipientId);

        Assert.True(accepted.Result.IsSuccess);
        Assert.Equal(issued.TenantId, accepted.Result.Value.OrganizationId);
        Assert.Equal(kind == "none", accepted.Result.Value.CreatedAccount);
        Assert.Equal(usersBefore + (kind == "none" ? 1 : 0), await db.Users.CountAsync(Ct));
        Assert.Equal(personalBefore, await db.Tenants.CountAsync(row => row.Kind == TenantKind.Personal, Ct));
        Assert.Equal(legalBefore + (kind == "none" ? 2 : 0), await db.LegalAcceptances.CountAsync(Ct));
        using var tenantScope = check.ServiceProvider.GetRequiredService<ITenantScope>().Enter(issued.TenantId);
        var row = await check.ServiceProvider.GetRequiredService<IInvitationReader>().FindByIdAsync(issued.Id, Ct);
        Assert.NotNull(row);
        Assert.Equal(InvitationStatus.Accepted, row.Status);
        Assert.Equal(MemberStatus.Active, row.MemberStatus);
        var identity = Assert.Single(await db.LoginMethods.AsNoTracking()
            .Where(method => method.Type == LoginMethodType.Email && method.Value == issued.Email.Value).ToArrayAsync(Ct));
        Assert.Equal(row.AcceptedByUserId, identity.UserId);
        Assert.NotNull(identity.VerifiedAtUtc);
        Assert.Equal(kind != "google", identity.IsPrimary);
        Assert.Equal(kind == "none", row.BootstrapNonceHash is not null);
        var accesses = await check.ServiceProvider.GetRequiredService<IUserTenantAccessReader>().ListForUserAsync(identity.UserId, Ct);
        Assert.Equal(MemberStatus.Active, Assert.Single(accesses, access => access.TenantId == issued.TenantId).MemberStatus);
        Assert.DoesNotContain(accesses, access => access.Kind == TenantKind.Personal);
        Assert.Equal(kind == "none", accepted.Cookies.Any(cookie => cookie.StartsWith(".AspNetCore.Identity.Application=", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Existing_account_requires_its_own_session_and_is_never_logged_in_by_the_link(bool otherSession)
    {
        using var client = factory.CreateClient();
        var issued = await IssueAsync("email");
        Guid? current = otherSession ? await CreateAccountAsync(Email.Create($"other-{Guid.NewGuid():N}@example.test").Value) : null;

        var accepted = await AcceptAsync(issued, current);

        Assert.Equal(otherSession ? InvitationErrors.WrongAccount : InvitationErrors.SignInRequired, accepted.Result.Error);
        Assert.DoesNotContain(accepted.Cookies, cookie => cookie.StartsWith(".AspNetCore.Identity.Application=", StringComparison.Ordinal));
        await AssertPendingAsync(issued);
    }

    [Fact]
    public async Task New_identity_requires_legal_consent_before_writing()
    {
        using var client = factory.CreateClient();
        var issued = await IssueAsync("none");
        var accepted = await AcceptAsync(issued, null, acceptedTerms: false);
        Assert.IsType<ValidationError>(accepted.Result.Error);
        await AssertPendingAsync(issued);
    }

    [Fact]
    public async Task Account_registered_between_preview_and_accept_requires_sign_in_instead_of_a_duplicate()
    {
        using var client = factory.CreateClient();
        var issued = await IssueAsync("none");
        var preview = await PreviewAsync(issued, null);
        Assert.Equal(InvitationPreviewState.NoAccount, preview.Result.Value.State);
        var continuation = preview.Cookies.Single(cookie => cookie.StartsWith("__Host-MtInvitation=", StringComparison.Ordinal));
        await CreateAccountAsync(issued.Email);
        var accepted = await AcceptAsync(issued, null, continuation: continuation, sendToken: false);
        Assert.Equal(InvitationErrors.SignInRequired, accepted.Result.Error);
        await AssertPendingAsync(issued);
    }

    [Fact]
    public async Task Invitation_recovers_an_unverified_foreign_email_without_binding_the_foreign_identity()
    {
        using var client = factory.CreateClient();
        var issued = await IssueAsync("none");
        var foreign = await CreateAccountAsync(Email.Create($"foreign-{Guid.NewGuid():N}@example.test").Value);
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
            {
                scope.ServiceProvider.GetRequiredService<ILoginMethodRepository>().Add(LoginMethod.CreateEmail(foreign, issued.Email));
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        var accepted = await AcceptAsync(issued, null);
        Assert.True(accepted.Result.IsSuccess);
        await using var check = factory.Services.CreateAsyncScope();
        var method = Assert.Single(await check.ServiceProvider.GetRequiredService<ApplicationDbContext>().LoginMethods
            .AsNoTracking().Where(row => row.Type == LoginMethodType.Email && row.Value == issued.Email.Value).ToArrayAsync(Ct));
        Assert.NotEqual(foreign, method.UserId);
        Assert.NotNull(method.VerifiedAtUtc);
    }

    [Fact]
    public async Task Simultaneous_accepts_have_one_winner_one_identity_and_two_legal_acceptances()
    {
        using var client = factory.CreateClient();
        var issued = await IssueAsync("none");
        await using var check = factory.Services.CreateAsyncScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var usersBefore = await db.Users.CountAsync(Ct);
        var legalBefore = await db.LegalAcceptances.CountAsync(Ct);

        var results = await Task.WhenAll(AcceptAsync(issued, null), AcceptAsync(issued, null));

        Assert.Single(results, result => result.Result.IsSuccess);
        Assert.Equal(InvitationErrors.AlreadyUsed, Assert.Single(results, result => result.Result.IsFailure).Result.Error);
        Assert.Equal(usersBefore + 1, await db.Users.CountAsync(Ct));
        Assert.Equal(legalBefore + 2, await db.LegalAcceptances.CountAsync(Ct));
    }

    [Fact]
    public async Task Lost_accept_response_can_restore_only_the_original_new_identity_browser()
    {
        using var client = factory.CreateClient();
        var issued = await IssueAsync("none");
        var accepted = await AcceptAsync(issued, null);
        Assert.True(accepted.Result.IsSuccess);

        var original = await PreviewAsync(issued, accepted.ContinuationCookie);
        var other = await PreviewAsync(issued, null);

        Assert.Equal(InvitationPreviewState.Accepted, original.Result.Value.State);
        Assert.Contains(original.Cookies, cookie => cookie.StartsWith(".AspNetCore.Identity.Application=", StringComparison.Ordinal));
        Assert.Equal(InvitationPreviewState.Invalid, other.Result.Value.State);
        Assert.DoesNotContain(other.Cookies, cookie => cookie.StartsWith(".AspNetCore.Identity.Application=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ambiguous_verified_email_owners_cannot_choose_or_create_an_identity()
    {
        using var client = factory.CreateClient();
        var issued = await IssueAsync("email");
        await CreateAccountAsync(issued.Email, google: true);
        var accepted = await AcceptAsync(issued, issued.RecipientId);
        Assert.Equal(InvitationErrors.WrongAccount, accepted.Result.Error);
        await AssertPendingAsync(issued);
    }

    [Theory]
    [InlineData("Suspended")]
    [InlineData("PendingDeletion")]
    [InlineData("Deleted")]
    public async Task Inactive_identity_cannot_accept_or_be_duplicated(string state)
    {
        using var client = factory.CreateClient();
        var issued = await IssueAsync("email");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var user = await services.GetRequiredService<ApplicationDbContext>().Users.SingleAsync(row => row.Id == issued.RecipientId, ct);
                var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
                Assert.True((state == "Suspended" ? user.Suspend() : user.RequestDeletion("Test", nowUtc.AddDays(-31), 30)).IsSuccess);
                if (state == "Deleted") Assert.True(user.CompleteDeletion(nowUtc, "Cuenta eliminada").IsSuccess);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        var accepted = await AcceptAsync(issued, issued.RecipientId);
        Assert.Equal(state == "PendingDeletion" ? AccountErrors.PendingDeletion : AccountErrors.Suspended, accepted.Result.Error);
        await AssertPendingAsync(issued);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("revoked")]
    [InlineData("suspended")]
    public async Task Acceptance_rechecks_invitation_and_organization_before_writing(string changed)
    {
        using var client = factory.CreateClient();
        var issued = await IssueAsync("none");
        await using (var connection = await RuntimeRoleConnection.OpenAsync(factory.RuntimeConnectionString, issued.TenantId, Ct))
        {
            var sql = changed switch
            {
                "expired" => "UPDATE tenant.\"Invitations\" SET \"ExpiresAtUtc\" = @now WHERE \"Id\" = @id",
                "revoked" => "UPDATE tenant.\"Invitations\" SET \"Status\" = 'Revoked' WHERE \"Id\" = @id",
                _ => "UPDATE platform.\"Tenants\" SET \"Status\" = 'Suspended' WHERE \"Id\" = @id",
            };
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("id", changed == "suspended" ? issued.TenantId : issued.Id);
            command.Parameters.AddWithValue("now", factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime.AddSeconds(-1));
            await command.ExecuteNonQueryAsync(Ct);
        }
        // El navegador conserva el enlace original aunque cambie el estado antes del POST.
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Scheme = "https";
        var accessor = services.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = context;
        try
        {
            var accepted = await services.GetRequiredService<IInvitationService>().AcceptAsync(new(issued.Token, true), Ct);
            Assert.Equal(changed switch { "expired" => InvitationErrors.Expired, "revoked" => InvitationErrors.Invalid, _ => TenantErrors.Suspended }, accepted.Error);
        }
        finally { accessor.HttpContext = null; }
    }

    [Fact]
    public async Task Bootstrap_grant_expiry_does_not_restore_a_session_even_with_the_original_cookie()
    {
        using var client = factory.CreateClient();
        var issued = await IssueAsync("none");
        var accepted = await AcceptAsync(issued, null);
        Assert.True(accepted.Result.IsSuccess);
        await using var connection = await RuntimeRoleConnection.OpenAsync(factory.RuntimeConnectionString, issued.TenantId, Ct);
        await using var command = new NpgsqlCommand("UPDATE tenant.\"Invitations\" SET \"AcceptedAtUtc\" = @earlier WHERE \"Id\" = @id", connection);
        command.Parameters.AddWithValue("id", issued.Id);
        command.Parameters.AddWithValue("earlier", factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime.AddMinutes(-6));
        await command.ExecuteNonQueryAsync(Ct);

        var preview = await PreviewAsync(issued, accepted.ContinuationCookie);

        Assert.Equal(InvitationPreviewState.Invalid, preview.Result.Value.State);
        Assert.DoesNotContain(preview.Cookies, cookie => cookie.StartsWith(".AspNetCore.Identity.Application=", StringComparison.Ordinal));
    }

    private async Task AssertPendingAsync(Issued issued)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(issued.TenantId);
        var row = await scope.ServiceProvider.GetRequiredService<IInvitationReader>().FindByIdAsync(issued.Id, Ct);
        Assert.Equal(InvitationStatus.Pending, row!.Status);
        Assert.Equal(MemberStatus.Invited, row.MemberStatus);
    }

    private async Task<(Result<AcceptInvitationResponse> Result, string[] Cookies, string ContinuationCookie)> AcceptAsync(
        Issued issued, Guid? currentUserId, bool acceptedTerms = true, string? continuation = null, bool sendToken = true)
    {
        if (continuation is null)
        {
            var preview = await PreviewAsync(issued, null, currentUserId);
            continuation = preview.Cookies.Single(cookie => cookie.StartsWith("__Host-MtInvitation=", StringComparison.Ordinal));
        }
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var accessor = services.GetRequiredService<IHttpContextAccessor>();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Scheme = "https";
        context.Request.Headers.Cookie = continuation.Split(';')[0];
        if (currentUserId is { } current)
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", current.ToString())], "Integration"));
        accessor.HttpContext = context;
        try
        {
            var service = services.GetRequiredService<IInvitationService>();
            var accepted = await service.AcceptAsync(new AcceptInvitationRequest(sendToken ? issued.Token : null, acceptedTerms), Ct);
            return (accepted, context.Response.Headers.SetCookie.Select(value => value!).ToArray(), continuation.Split(';')[0]);
        }
        finally { accessor.HttpContext = null; }
    }

    private async Task<(Result<InvitationPreviewResponse> Result, string[] Cookies)> PreviewAsync(Issued issued, string? cookie,
        Guid? currentUserId = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Scheme = "https";
        if (currentUserId is { } current)
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", current.ToString())], "Integration"));
        if (cookie is not null) context.Request.Headers.Cookie = cookie;
        var accessor = services.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = context;
        try
        {
            var preview = await services.GetRequiredService<IInvitationService>()
                .PreviewAsync(new PreviewInvitationRequest(cookie is null ? issued.Token : null), Ct);
            return (preview, context.Response.Headers.SetCookie.Select(value => value!).ToArray());
        }
        finally { accessor.HttpContext = null; }
    }

    private async Task<Issued> IssueAsync(string recipientKind)
    {
        var email = Email.Create($"accept-{Guid.NewGuid():N}@example.test").Value;
        Guid? recipient = recipientKind == "none" ? null : await CreateAccountAsync(email, recipientKind == "google");
        var inviter = await CreateAccountAsync(Email.Create($"inviter-{Guid.NewGuid():N}@example.test").Value);
        var tenant = Tenant.CreateBusiness("Invitations acceptance", requiresApproval: false);
        Assert.True(tenant.Activate().IsSuccess);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenant.Id);
        var uow = services.GetRequiredService<IUnitOfWork>();
        await uow.ExecuteInTransactionAsync(ct =>
        {
            services.GetRequiredService<TenantSpaceProvisioner>().Stage(tenant,
                TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"), [inviter]);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        await uow.ExecuteInTransactionAsync(async ct =>
        {
            var legal = services.GetRequiredService<ILegalRepository>();
            var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
            foreach (var kind in Enum.GetValues<LegalDocumentKind>())
                if (await legal.GetCurrentDocumentAsync(kind, nowUtc, ct) is null)
                {
                    var document = LegalDocument.Create(kind, 1, nowUtc.AddDays(-1));
                    legal.AddDocument(document);
                    legal.AddContent(LegalDocumentContent.Create(document.Id, "es-AR", "Documento del test de invitaciones."));
                    legal.AddContent(LegalDocumentContent.Create(document.Id, "en-US", "Invitation test document."));
                }
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        var issued = await uow.ExecuteInTransactionAsync(ct => services.GetRequiredService<InvitationIssuer>()
            .IssueAsync(new IssueInvitationRequest(email, inviter, InvitationChannel.Email), ct), CommitPolicy.OnSuccess, Ct);
        Assert.True(issued.IsSuccess);
        var outbox = await services.GetRequiredService<ApplicationDbContext>().OutboxMessages.AsNoTracking()
            .OrderByDescending(row => row.CreatedAtUtc).ThenByDescending(row => row.Id).FirstAsync(Ct);
        var message = JsonSerializer.Deserialize<EmailMessage>(services.GetRequiredService<IPayloadProtector>().Unprotect(outbox.EncryptedPayload))!;
        var token = Regex.Match(message.TextBody, @"/invitacion#(?<token>[A-Za-z0-9_-]+)").Groups["token"].Value;
        Assert.NotEmpty(token);
        return new Issued(tenant.Id, issued.Value, email, token, recipient);
    }

    private async Task<Guid> CreateAccountAsync(Email email, bool google = false)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var created = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var user = await services.GetRequiredService<IUserRepository>().CreateAsync("Persona real", "es-AR", "America/Argentina/Buenos_Aires", ct);
            var method = google ? LoginMethod.CreateGoogle(user.Id, $"google-{Guid.NewGuid():N}", email) : LoginMethod.CreateEmail(user.Id, email);
            method.Verify(services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
            Assert.True(method.MakePrimary().IsSuccess);
            services.GetRequiredService<ILoginMethodRepository>().Add(method);
            if (!google) await services.GetRequiredService<IUserRepository>().SetPrimaryEmailAsync(user.Id, email, ct);
            return Result.Success(user.Id);
        }, CommitPolicy.OnSuccess, Ct);
        return created.Value;
    }

    private sealed record Issued(Guid TenantId, Guid Id, Email Email, string Token, Guid? RecipientId)
    {
        public override string ToString() => "Issued [redacted]";
    }
}
