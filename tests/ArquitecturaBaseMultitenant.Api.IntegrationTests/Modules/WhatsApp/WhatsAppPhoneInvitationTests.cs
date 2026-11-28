using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

/// <summary>
/// B5c de la E11 (decisión 11, ADR 0038 y 0047): «Cargar teléfonos» no dice si un número tiene cuenta e invita por WhatsApp;
/// una invitación por teléfono se acepta solo con ese teléfono verificado, también con el código de ingreso; sin el canal de
/// Plataforma el preview lo dice y no sale nada.
/// </summary>
public sealed class WhatsAppPhoneInvitationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Imports = "/api/organization/whatsapp/phone-imports";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Load_phones_never_reveals_an_account_and_invites_each_new_number_by_whatsapp()
    {
        await using var host = Host();
        using var client = WhatsAppAttentionJourney.Client(host);
        var (_, tenantId) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, client, Ct);
        var employee = WhatsAppAttentionJourney.NewPhone();
        await OrganizationJourney.AddActiveMemberAsync(factory, tenantId, await StagePhoneAsync(employee, "Marta Empleada"), Ct);
        var withAccount = WhatsAppAttentionJourney.NewPhone();
        await StagePhoneAsync(withAccount, "Otra persona");
        var fresh = WhatsAppAttentionJourney.NewPhone();
        var body = Body(Input(employee), Input(withAccount), Input(fresh), new { country = "AR", number = "123" }, Input(fresh),
            new { country = "US", number = "+12025550123" });

        var preview = await WhatsAppAttentionJourney.JsonAsync(await WhatsAppAttentionJourney.PostAsync(client, Imports + "/preview", body, Ct), Ct);
        var rows = preview.GetProperty("rows").EnumerateArray().ToArray();
        Assert.Equal([1, 2, 3, 4, 5, 6], rows.Select(row => row.GetProperty("row").GetInt32()));
        Assert.Equal(["AlreadyMember", "Invite", "Invite", "Invalid", "Invalid", "Invalid"], rows.Select(row => row.GetProperty("outcome").GetString()));
        Assert.Equal("Marta Empleada", rows[0].GetProperty("memberName").GetString());
        // Con cuenta o sin cuenta, la fila dice lo mismo (precisión del 11/10 de la 0038).
        Assert.All(rows[1..3], row => Assert.Equal(JsonValueKind.Null, row.GetProperty("memberName").ValueKind));
        Assert.Equal(JsonValueKind.Null, rows[3].GetProperty("phone").ValueKind);
        Assert.Equal((1, 2, 3), (preview.GetProperty("alreadyMembers").GetInt32(), preview.GetProperty("invites").GetInt32(),
            preview.GetProperty("invalid").GetInt32()));
        Assert.Empty(await InvitationMessagesAsync(host.Services, [employee, withAccount, fresh]));

        using var imported = await WhatsAppAttentionJourney.PostAsync(client, Imports, body, Ct);
        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
        var result = await WhatsAppAttentionJourney.JsonAsync(imported, Ct);
        Assert.Equal((1, 2, 3), (result.GetProperty("linked").GetInt32(), result.GetProperty("invited").GetInt32(),
            result.GetProperty("invalid").GetInt32()));
        Assert.Equal(new[] { withAccount, fresh }.Order(StringComparer.Ordinal),
            (await InvitationMessagesAsync(host.Services, [employee, withAccount, fresh])).Select(message => message.To).Order(StringComparer.Ordinal));

        // CC25: en Usuarios, la invitación por teléfono trae el teléfono y no un correo.
        var users = await WhatsAppAttentionJourney.JsonAsync(await client.GetAsync("/api/users/paged?pageSize=50", Ct), Ct);
        var invited = Assert.Single(users.GetProperty("items").EnumerateArray(), row => row.GetProperty("phone").GetString() == fresh);
        Assert.Equal(JsonValueKind.Null, invited.GetProperty("email").ValueKind);
        Assert.Equal("Invited", invited.GetProperty("state").GetString());
    }

    /// <summary>Decisión 179: sin «La persona aceptó recibir mensajes por WhatsApp» no sale ninguna invitación.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public async Task Without_the_consent_checkbox_nothing_is_saved_or_sent(bool? consent)
    {
        await using var host = Host();
        using var client = WhatsAppAttentionJourney.Client(host);
        var (_, tenantId) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, client, Ct);
        var fresh = WhatsAppAttentionJourney.NewPhone();
        var body = new { phones = new[] { Input(fresh) }, companyId = (Guid?)null, roleIds = Array.Empty<Guid>(), consentConfirmed = consent };

        using var previewed = await WhatsAppAttentionJourney.PostAsync(client, Imports + "/preview", body, Ct);
        Assert.Equal(HttpStatusCode.OK, previewed.StatusCode);
        using var refused = await WhatsAppAttentionJourney.PostAsync(client, Imports, body, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        var problem = await WhatsAppAttentionJourney.JsonAsync(refused, Ct);
        Assert.Equal("WhatsApp.PhoneImport.ConsentRequired", problem.GetProperty("code").GetString());
        Assert.Equal("Confirmá que la persona aceptó recibir mensajes por WhatsApp.",
            Assert.Single(problem.GetProperty("errors").GetProperty("consentConfirmed").EnumerateArray()).GetString());
        Assert.Empty(await InvitationMessagesAsync(host.Services, [fresh]));
        Assert.Empty(await InvitationsAsync(host.Services, tenantId));
    }

    [Fact]
    public async Task Each_invitation_keeps_who_confirmed_the_consent_and_when_also_when_resent()
    {
        await using var host = Host();
        using var client = WhatsAppAttentionJourney.Client(host);
        var (owner, tenantId) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, client, Ct);
        var before = host.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var phones = new[] { WhatsAppAttentionJourney.NewPhone(), WhatsAppAttentionJourney.NewPhone() };

        using (var imported = await WhatsAppAttentionJourney.PostAsync(client, Imports, Body([.. phones.Select(Input)]), Ct))
            Assert.Equal(HttpStatusCode.OK, imported.StatusCode);

        var invitations = await InvitationsAsync(host.Services, tenantId);
        Assert.Equal(2, invitations.Count);
        Assert.All(invitations, invitation =>
        {
            Assert.Equal(owner.UserId, invitation.ConsentConfirmedByUserId);
            Assert.InRange(invitation.ConsentConfirmedAtUtc!.Value, before, invitation.IssuedAtUtc);
        });
        Assert.Single(invitations.Select(invitation => invitation.ConsentConfirmedAtUtc).Distinct());

        // Reenviar no vuelve a preguntar: la invitación nueva conserva el consentimiento de la que reemplaza.
        var first = invitations[0];
        using (var resent = await WhatsAppAttentionJourney.PostAsync(client, $"/api/users/invitations/{first.Id:D}/resend", new { }, Ct))
            Assert.Equal(HttpStatusCode.OK, resent.StatusCode);
        var replacement = Assert.Single(await InvitationsAsync(host.Services, tenantId),
            invitation => invitation.MemberId == first.MemberId && invitation.Status == InvitationStatus.Pending);
        Assert.NotEqual(first.Id, replacement.Id);
        Assert.Equal((first.ConsentConfirmedByUserId, first.ConsentConfirmedAtUtc),
            (replacement.ConsentConfirmedByUserId, replacement.ConsentConfirmedAtUtc));
    }

    /// <summary>B6 (ADR 0066 §4): la invitación por WhatsApp sigue en Usuarios el estado de su mensaje en la cola.</summary>
    [Fact]
    public async Task A_phone_invitation_shows_each_queue_state_of_its_whatsapp_message_in_users()
    {
        await using var host = Host();
        using var client = WhatsAppAttentionJourney.Client(host);
        var (_, tenantId) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, client, Ct);
        var fresh = WhatsAppAttentionJourney.NewPhone();
        using (var imported = await WhatsAppAttentionJourney.PostAsync(client, Imports, Body(Input(fresh)), Ct))
            Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
        var invitation = Assert.Single(await InvitationsAsync(host.Services, tenantId),
            value => value.Destination.Phone?.Value == fresh);
        var outboxId = Assert.IsType<Guid>(invitation.OutboxMessageId);
        await using (var scope = host.Services.CreateAsyncScope())
            Assert.Equal("whatsapp", (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutboxMessages
                .AsNoTracking().SingleAsync(message => message.Id == outboxId, Ct)).Channel);

        await AssertDeliveryAsync("Pending");
        foreach (var (status, expected) in new[]
                 {
                     (OutboxStatus.Uncertain, "Uncertain"), (OutboxStatus.Sent, "Sent"), (OutboxStatus.Failed, "Failed"),
                     (OutboxStatus.Cancelled, "Failed"),
                 })
        {
            await OutboxRows.SetStatusAsync(host.Services, outboxId, status, Ct);
            await AssertDeliveryAsync(expected);
        }
        await OutboxRows.DeleteAsync(factory, outboxId, Ct);
        await AssertDeliveryAsync("Sent");

        async Task AssertDeliveryAsync(string expected)
        {
            var users = await WhatsAppAttentionJourney.JsonAsync(await client.GetAsync("/api/users/paged?pageSize=50", Ct), Ct);
            var row = Assert.Single(users.GetProperty("items").EnumerateArray(), item => item.GetProperty("phone").GetString() == fresh);
            Assert.Equal(expected, row.GetProperty("invitationDelivery").GetString());
            var detail = await WhatsAppAttentionJourney.JsonAsync(
                await client.GetAsync($"/api/users/{invitation.MemberId:D}", Ct), Ct);
            Assert.Equal(expected, detail.GetProperty("invitationDelivery").GetString());
        }
    }

    [Fact]
    public async Task A_phone_invitation_is_accepted_only_with_that_phone_verified()
    {
        await using var host = Host();
        using var client = WhatsAppAttentionJourney.Client(host);
        var (_, tenantId) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, client, Ct);
        var owned = WhatsAppAttentionJourney.NewPhone();
        var ownerOfPhone = await StagePhoneAsync(owned, "Dueña del teléfono");
        var intruder = await StagePhoneAsync(WhatsAppAttentionJourney.NewPhone(), "Otra cuenta");
        var fresh = WhatsAppAttentionJourney.NewPhone();
        using (var imported = await WhatsAppAttentionJourney.PostAsync(client, Imports, Body(Input(owned), Input(fresh)), Ct))
            Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
        var ownedToken = await TokenAsync(host.Services, owned);
        var freshToken = await TokenAsync(host.Services, fresh);

        // CC26: el preview muestra el teléfono, sin correo, y ofrece el código por WhatsApp.
        var signIn = await PreviewAsync(host.Services, ownedToken, null);
        Assert.Equal((InvitationPreviewState.SignInRequired, owned, true), (signIn.State, signIn.Phone, signIn.CanUsePhoneCode));
        Assert.Null(signIn.Email);
        Assert.Equal(InvitationPreviewState.WrongSession, (await PreviewAsync(host.Services, ownedToken, intruder)).State);
        Assert.Equal(InvitationErrors.WrongAccount, (await AcceptAsync(host.Services, ownedToken, intruder)).Error);
        await AssertInvitationAsync(host.Services, tenantId, owned, InvitationStatus.Pending, null);

        Assert.True((await AcceptAsync(host.Services, ownedToken, ownerOfPhone)).IsSuccess);
        await AssertInvitationAsync(host.Services, tenantId, owned, InvitationStatus.Accepted, ownerOfPhone);

        // ADR 0047 por teléfono: el código por WhatsApp crea la cuenta con ese teléfono y acepta.
        var noAccount = await PreviewAsync(host.Services, freshToken, null);
        Assert.Equal((InvitationPreviewState.NoAccount, fresh, true), (noAccount.State, noAccount.Phone, noAccount.CanUsePhoneCode));
        using var anonymous = WhatsAppAttentionJourney.Client(host);
        using (var requested = await WhatsAppIdentityTests.PostAsync(anonymous, "/api/auth/login-code", Contact(fresh), Ct))
            Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await WhatsAppIdentityTests.CodeAsync(host.Services, fresh);
        using (var verified = await WhatsAppIdentityTests.PostAsync(anonymous, "/api/auth/login-code/verify",
            new { phone = new { country = "AR", number = fresh }, channel = "whatsapp", code, returnUrl = "/connect/authorize?client_id=web&access=business" }, Ct))
            Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        await using var scope = host.Services.CreateAsyncScope();
        var created = await scope.ServiceProvider.GetRequiredService<IUserLookup>().FindVerifiedUserIdAsync(LoginMethodType.Phone, fresh, Ct);
        Assert.NotNull(created);
        await AssertInvitationAsync(host.Services, tenantId, fresh, InvitationStatus.Accepted, created);
    }

    [Fact]
    public async Task Without_the_platform_channel_the_preview_says_so_and_nothing_is_sent()
    {
        await using var host = Host();
        using var client = WhatsAppAttentionJourney.Client(host);
        await WhatsAppAttentionJourney.OrganizationAsync(factory, host, client, Ct);
        var fresh = WhatsAppAttentionJourney.NewPhone();
        using (var imported = await WhatsAppAttentionJourney.PostAsync(client, Imports, Body(Input(fresh)), Ct))
            Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
        var token = await TokenAsync(host.Services, fresh);
        var later = WhatsAppAttentionJourney.NewPhone();
        var sent = (await InvitationMessagesAsync(host.Services, [fresh, later])).Count;
        await SetPlatformStatusAsync(host.Services, "Connected", "PaymentMissing");
        try
        {
            var preview = await PreviewAsync(host.Services, token, null);
            Assert.Equal((InvitationPreviewState.NoAccount, false), (preview.State, preview.CanUsePhoneCode));
            using var blocked = await WhatsAppAttentionJourney.PostAsync(client, Imports, Body(Input(later)), Ct);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, blocked.StatusCode);
            Assert.Equal(InvitationErrors.ChannelUnavailableCode, (await WhatsAppAttentionJourney.JsonAsync(blocked, Ct)).GetProperty("code").GetString());
            Assert.Equal(sent, (await InvitationMessagesAsync(host.Services, [fresh, later])).Count);
        }
        finally { await SetPlatformStatusAsync(host.Services, "PaymentMissing", "Connected"); }
    }

    /// <summary>B9 (decisión 1 de los arreglos de la revisión): tope diario por organización y global de «Cargar teléfonos».</summary>
    [Fact]
    public async Task A_batch_over_the_daily_limit_answers_429_without_saving_or_enqueuing_and_another_organization_still_imports()
    {
        await using var host = Host(new Dictionary<string, string>
        {
            ["WhatsApp:Quotas:DailyInvitationLimitPerOrganization"] = "2",
            ["WhatsApp:Quotas:DailyInvitationLimit"] = "3",
        });
        await ClearWhatsAppInvitationGlobalQuotaAsync(host.Services);
        try
        {
            using var firstClient = WhatsAppAttentionJourney.Client(host);
            var (_, firstTenantId) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, firstClient, Ct);
            using var secondClient = WhatsAppAttentionJourney.Client(host);
            var (_, secondTenantId) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, secondClient, Ct);
            using var thirdClient = WhatsAppAttentionJourney.Client(host);
            var (_, thirdTenantId) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, thirdClient, Ct);

            // Primera organización: 1 invitación entra (1/2 de la org, 1/3 global).
            var firstPhone = WhatsAppAttentionJourney.NewPhone();
            using (var ok = await WhatsAppAttentionJourney.PostAsync(firstClient, Imports, Body(Input(firstPhone)), Ct))
                Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
            Assert.Single(await InvitationsAsync(host.Services, firstTenantId));
            Assert.Single(await InvitationMessagesAsync(host.Services, [firstPhone]));

            // Primera organización: una carga de 2 pasa su tope (1 + 2 > 2) → 429 con retryAfter y Retry-After, sin guardar ni encolar nada.
            var rejectedFirst = WhatsAppAttentionJourney.NewPhone();
            var rejectedSecond = WhatsAppAttentionJourney.NewPhone();
            using (var limited = await WhatsAppAttentionJourney.PostAsync(firstClient, Imports,
                       Body(Input(rejectedFirst), Input(rejectedSecond)), Ct))
                await AssertDailyLimitAsync(limited);
            Assert.Empty(await InvitationMessagesAsync(host.Services, [rejectedFirst, rejectedSecond]));
            Assert.Single(await InvitationsAsync(host.Services, firstTenantId));

            // Otra organización sigue entrando con su propio tope hasta completar el global (1 + 2 = 3).
            var secondPhone1 = WhatsAppAttentionJourney.NewPhone();
            var secondPhone2 = WhatsAppAttentionJourney.NewPhone();
            using (var secondOk = await WhatsAppAttentionJourney.PostAsync(secondClient, Imports,
                       Body(Input(secondPhone1), Input(secondPhone2)), Ct))
                Assert.Equal(HttpStatusCode.OK, secondOk.StatusCode);
            Assert.Equal(2, (await InvitationsAsync(host.Services, secondTenantId)).Count);
            Assert.Equal(2, (await InvitationMessagesAsync(host.Services, [secondPhone1, secondPhone2])).Count);

            // Tercera organización: el global ya está en 3/3 → 429 sin guardar ni encolar nada.
            var thirdPhone = WhatsAppAttentionJourney.NewPhone();
            using (var globalLimited = await WhatsAppAttentionJourney.PostAsync(thirdClient, Imports, Body(Input(thirdPhone)), Ct))
                await AssertDailyLimitAsync(globalLimited);
            Assert.Empty(await InvitationMessagesAsync(host.Services, [thirdPhone]));
            Assert.Empty(await InvitationsAsync(host.Services, thirdTenantId));
        }
        finally
        {
            await ClearWhatsAppInvitationGlobalQuotaAsync(host.Services);
        }
    }

    private WebApplicationFactory<Program> Host(IReadOnlyDictionary<string, string>? extraSettings = null)
    {
        var settings = new Dictionary<string, string>
        {
            ["WhatsApp:PhoneNumberId"] = "phone-invitations",
            ["WhatsApp:AccessToken"] = "fixture-token",
            ["WhatsApp:AllowedCountries:0"] = "AR",
            ["Authentication:LoginCode:ResendCooldownSeconds"] = "0",
            ["Authentication:LoginCode:MaxRequestsPerWindow"] = "100",
        };
        if (extraSettings is not null)
            foreach (var (key, value) in extraSettings) settings[key] = value;
        return WhatsAppAttentionJourney.Host(factory, new FakeGraph(), settings: settings)
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(WhatsAppIdentityTestTransport.BlockExternalSender));
    }

    private static async Task AssertDailyLimitAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        var problem = await WhatsAppAttentionJourney.JsonAsync(response, Ct);
        Assert.Equal(WhatsAppErrors.PhoneImportDailyLimitCode, problem.GetProperty("code").GetString());
        Assert.Equal("Por ahora no se pueden mandar más invitaciones por WhatsApp, y no se guardó nada. Probá de nuevo más tarde.",
            problem.GetProperty("detail").GetString());
        var seconds = problem.GetProperty(WhatsAppErrors.RetryAfterKey).GetInt32();
        Assert.InRange(seconds, 24 * 3600, 25 * 3600);
        Assert.Equal(seconds.ToString(CultureInfo.InvariantCulture), Assert.Single(response.Headers.GetValues("Retry-After")));
    }

    private static async Task ClearWhatsAppInvitationGlobalQuotaAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var scopeName = MessageQuotaScope.WhatsAppInvitationGlobal.ToString();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.ExecuteSqlAsync(
            $"""DELETE FROM identity."MessageQuotaBuckets" WHERE "Scope" = {scopeName}""", Ct);
    }

    private static object Input(string phone) => new { country = "AR", number = phone };
    private static object Body(params object[] phones) =>
        new { phones, companyId = (Guid?)null, roleIds = Array.Empty<Guid>(), consentConfirmed = true };
    private static object Contact(string phone) => new { phone = new { country = "AR", number = phone }, channel = "whatsapp" };

    private async Task<Guid> StagePhoneAsync(string phone, string name)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var created = await provider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var value = PhoneNumber.Create(phone).Value;
            await provider.GetRequiredService<IExternalLoginLock>().LockPhoneAsync(value, ct);
            var user = await provider.GetRequiredService<IUserRepository>().CreateAsync(name, "es-AR", "America/Argentina/Buenos_Aires", ct);
            var method = LoginMethod.CreatePhone(user.Id, value);
            Assert.True((await provider.GetRequiredService<LoginMethodLinker>().VerifyAsync(method,
                provider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime, ct)).IsSuccess);
            provider.GetRequiredService<ILoginMethodRepository>().Add(method);
            return Result.Success(user.Id);
        }, CommitPolicy.OnSuccess, Ct);
        return created.Value;
    }

    /// <summary>Las invitaciones por WhatsApp a los teléfonos del caso: la base es de toda la clase y otros casos también invitan.</summary>
    private static async Task<IReadOnlyList<(string To, string Button)>> InvitationMessagesAsync(IServiceProvider services, IReadOnlyCollection<string> phones)
    {
        await using var scope = services.CreateAsyncScope();
        var protector = scope.ServiceProvider.GetRequiredService<IPayloadProtector>();
        var messages = new List<(string, string)>();
        foreach (var row in await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutboxMessages.AsNoTracking()
            .Where(value => value.Channel == "whatsapp").ToArrayAsync(Ct))
        {
            // Lo que ya intentó salir no conserva el payload: el lector del pickup lo pasa por el transporte prohibido, que
            // lanza y lo deja «Sin confirmar» (ADR 0066).
            if (string.IsNullOrEmpty(row.EncryptedPayload)) continue;
            using var payload = JsonDocument.Parse(protector.Unprotect(row.EncryptedPayload));
            var root = payload.RootElement;
            if (root.GetProperty("TemplateName").GetString() == "invitacion_organizacion" && phones.Contains(root.GetProperty("To").GetString()!))
                messages.Add((root.GetProperty("To").GetString()!, root.GetProperty("ButtonParameter").GetString()!));
        }
        return messages;
    }

    private static async Task<IReadOnlyList<Invitation>> InvitationsAsync(IServiceProvider services, Guid tenantId)
    {
        await using var scope = services.CreateAsyncScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Invitations.AsNoTracking()
            .OrderBy(invitation => invitation.IssuedAtUtc).ThenBy(invitation => invitation.Id).ToListAsync(Ct);
    }

    private static async Task<string> TokenAsync(IServiceProvider services, string phone)
    {
        var button = Assert.Single(await InvitationMessagesAsync(services, [phone])).Button;
        return button[(button.IndexOf('#', StringComparison.Ordinal) + 1)..];
    }

    private static async Task<InvitationPreviewResponse> PreviewAsync(IServiceProvider services, string token, Guid? currentUserId)
    {
        await using var scope = services.CreateAsyncScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = Context(scope.ServiceProvider, currentUserId);
        try
        {
            var preview = await scope.ServiceProvider.GetRequiredService<IInvitationService>().PreviewAsync(new PreviewInvitationRequest(token), Ct);
            Assert.True(preview.IsSuccess);
            return preview.Value;
        }
        finally { accessor.HttpContext = null; }
    }

    private static async Task<Result<AcceptInvitationResponse>> AcceptAsync(IServiceProvider services, string token, Guid currentUserId)
    {
        await using var scope = services.CreateAsyncScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var previewContext = Context(scope.ServiceProvider, currentUserId);
        accessor.HttpContext = previewContext;
        try
        {
            await scope.ServiceProvider.GetRequiredService<IInvitationService>().PreviewAsync(new PreviewInvitationRequest(token), Ct);
            var continuation = Assert.Single(previewContext.Response.Headers.SetCookie,
                cookie => cookie!.StartsWith("__Host-MtInvitation=", StringComparison.Ordinal))!.Split(';')[0];
            var context = Context(scope.ServiceProvider, currentUserId);
            context.Request.Headers.Cookie = continuation;
            accessor.HttpContext = context;
            return await scope.ServiceProvider.GetRequiredService<IInvitationService>().AcceptAsync(new AcceptInvitationRequest(token, true), Ct);
        }
        finally { accessor.HttpContext = null; }
    }

    private static DefaultHttpContext Context(IServiceProvider services, Guid? currentUserId)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Scheme = "https";
        if (currentUserId is { } current)
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", current.ToString())], "Integration"));
        return context;
    }

    private static async Task AssertInvitationAsync(IServiceProvider services, Guid tenantId, string phone, InvitationStatus status, Guid? acceptedBy)
    {
        await using var scope = services.CreateAsyncScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
        var destination = InvitationIdentity.ForPhone(PhoneNumber.Create(phone).Value);
        var invitation = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Invitations.AsNoTracking()
            .Where(row => row.Destination == destination).OrderByDescending(row => row.IssuedAtUtc).FirstAsync(Ct);
        Assert.Equal((status, "whatsapp"), (invitation.Status, invitation.Channel));
        var row = await scope.ServiceProvider.GetRequiredService<IInvitationReader>().FindByIdAsync(invitation.Id, Ct);
        Assert.Equal(acceptedBy is null ? MemberStatus.Invited : MemberStatus.Active, row!.MemberStatus);
        Assert.Equal(acceptedBy, invitation.AcceptedByUserId);
    }

    private async Task SetPlatformStatusAsync(IServiceProvider services, string from, string to)
    {
        await using (var connection = new NpgsqlConnection(factory.AdminConnectionString))
        {
            await connection.OpenAsync(Ct);
            await using var command = new NpgsqlCommand(
                "UPDATE identity.\"WhatsAppChannels\" SET \"Status\" = @to WHERE \"OrganizationId\" IS NULL AND \"Status\" = @from", connection);
            command.Parameters.AddWithValue("from", from);
            command.Parameters.AddWithValue("to", to);
            Assert.Equal(1, await command.ExecuteNonQueryAsync(Ct));
        }
        await services.GetRequiredService<IWhatsAppChannelCache>().RefreshAsync(Ct);
    }
}
