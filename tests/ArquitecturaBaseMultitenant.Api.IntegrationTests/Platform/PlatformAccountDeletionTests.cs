using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Platform;

/// <summary>Baja iniciada por Plataforma (ADR 0035): gracia, operador guardado, aviso y reglas que la bloquean.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class PlatformAccountDeletionTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_active_account_goes_to_pending_deletion_with_the_operator_and_the_person_can_cancel()
    {
        using var platform = Client(factory);
        var operatorAccount = await AccountJourney.RegisterAsync(factory, platform, Ct);
        await PlatformOperators.GrantAsync(factory.Services, operatorAccount.UserId, PlatformOperators.All, Ct);
        await AccountJourney.AuthorizeAsync(platform, "platform", Ct);
        using var person = Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, person, Ct);
        var path = $"/api/platform/accounts/{account.UserId:D}/deletion";
        var outboxBefore = await OutboxIdsAsync(account.UserId);

        using var withoutReason = await PlatformJourney.PostAsync(platform, path,
            new { reason = " " }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, withoutReason.StatusCode);
        using var requested = await PlatformJourney.PostAsync(platform, path,
            new { reason = "Pedido legal recibido por correo." }, Ct);

        Assert.Equal(HttpStatusCode.NoContent, requested.StatusCode);
        using var me = await person.GetAsync("/api/me", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        var detail = await DetailAsync(platform, account.UserId);
        Assert.Equal("PendingDeletion", detail.GetProperty("state").GetString());
        var deletion = detail.GetProperty("deletion");
        Assert.True(deletion.GetProperty("requestedByPlatform").GetBoolean());
        Assert.Equal(TimeSpan.FromDays(30), deletion.GetProperty("scheduledForUtc").GetDateTime()
            - deletion.GetProperty("requestedAtUtc").GetDateTime());
        using var again = await PlatformJourney.PostAsync(platform, path,
            new { reason = "Otra vez" }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("Legal.AccountDeletion.AlreadyPending", await CodeAsync(again));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var db = services.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.AsNoTracking().SingleAsync(item => item.Id == account.UserId, Ct);
            Assert.Equal(operatorAccount.UserId, user.DeletionRequestedByOperatorId);
            // Los métodos siguen reservados durante la gracia.
            Assert.True(await db.LoginMethods.AnyAsync(method => method.UserId == account.UserId, Ct));
            var events = await db.SecurityEvents.AsNoTracking()
                .Where(item => item.TargetAccountId == account.UserId && item.Type == SecurityEventType.AccountDeletionRequested)
                .ToArrayAsync(Ct);
            var platformEvent = Assert.Single(events);
            Assert.Equal(AuditActorKind.PlatformOperator, platformEvent.ActorKind);
            Assert.Equal(operatorAccount.UserId, platformEvent.ActorId);
            Assert.Equal("Pedido legal recibido por correo.", platformEvent.Reason);
            var notice = await NoticeAsync(services, account.UserId, outboxBefore);
            Assert.Equal("La plataforma inició la baja de tu cuenta", notice.Subject);
            Assert.Contains("y cerramos todas tus sesiones.", notice.TextBody);
            Assert.Contains("Ingresar:", notice.TextBody);
            Assert.Contains("Si creés que es un error, ingresá antes de esa fecha para cancelarla.", notice.TextBody);
            Assert.DoesNotContain("Pedido legal", notice.TextBody);
        }

        var cancelTicket = await PendingDeletionTicketAsync(person, account);
        using var cancelled = await AccountJourney.PostAsync(person, "/api/auth/deletion/cancel", new { cancelTicket }, Ct);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.Equal("Active", (await DetailAsync(platform, account.UserId)).GetProperty("state").GetString());
    }

    [Fact]
    public async Task A_suspended_account_keeps_its_state_with_the_date_and_reactivation_returns_it_to_pending()
    {
        using var platform = Client(factory);
        var operatorAccount = await AccountJourney.RegisterAsync(factory, platform, Ct);
        await PlatformOperators.GrantAsync(factory.Services, operatorAccount.UserId, PlatformOperators.All, Ct);
        await AccountJourney.AuthorizeAsync(platform, "platform", Ct);
        using var person = Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, person, Ct);
        var accounts = $"/api/platform/accounts/{account.UserId:D}";
        using var suspended = await PlatformJourney.PostAsync(platform, accounts + "/suspend",
            new { reason = "Denuncia" }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, suspended.StatusCode);
        var support = await ConfigureSupportEmailAsync(platform);
        var outboxBefore = await OutboxIdsAsync(account.UserId);

        using var requested = await PlatformJourney.PostAsync(platform, accounts + "/deletion",
            new { reason = "Cierre por denuncia" }, Ct);

        Assert.Equal(HttpStatusCode.NoContent, requested.StatusCode);
        var detail = await DetailAsync(platform, account.UserId);
        Assert.Equal("Suspended", detail.GetProperty("state").GetString());
        Assert.True(detail.GetProperty("deletion").GetProperty("requestedByPlatform").GetBoolean());
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var notice = await NoticeAsync(scope.ServiceProvider, account.UserId, outboxBefore);
            Assert.Equal("La plataforma inició la baja de tu cuenta", notice.Subject);
            // Las sesiones ya se cerraron al suspenderla y una cuenta suspendida no puede entrar: sin botón.
            Assert.DoesNotContain("cerramos todas tus sesiones", notice.TextBody);
            Assert.DoesNotContain("Ingresar:", notice.TextBody);
            Assert.Contains("Tu cuenta está suspendida, así que no podés cancelar la baja. Si creés que es un error, escribí a "
                + support + ".", notice.TextBody);
        }
        await AgeLoginCodesAsync(account.Email.Value);
        using var login = await AccountJourney.PostAsync(person, "/api/auth/login-code", new { email = account.Email.Value }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, login.StatusCode);
        var code = await PickupCodeReader.ReadAsync(factory.Services, account.Email.Value, Ct);
        using var verified = await AccountJourney.PostAsync(person, "/api/auth/login-code/verify",
            new { email = account.Email.Value, code, returnUrl = "/invitacion" }, Ct);
        Assert.Equal(AccountErrors.SuspendedCode, await CodeAsync(verified));

        using var reactivated = await PlatformJourney.PostAsync(platform, accounts + "/reactivate",
            new { reason = "Se revisó" }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, reactivated.StatusCode);
        Assert.Equal("PendingDeletion", (await DetailAsync(platform, account.UserId)).GetProperty("state").GetString());
    }

    [Fact]
    public async Task The_sole_owner_of_an_open_organization_the_last_manager_and_the_own_account_are_protected()
    {
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        using var manager = Client(isolated);
        var managerAccount = await AccountJourney.RegisterAsync(isolated, manager, Ct);
        await PlatformOperators.GrantAsync(isolated.Services, managerAccount.UserId, PlatformOperators.All, Ct);
        using var support = Client(isolated);
        var supportAccount = await AccountJourney.RegisterAsync(isolated, support, Ct);
        await PlatformOperators.GrantAsync(isolated.Services, supportAccount.UserId,
            [PlatformPermissions.Accounts.Read, PlatformPermissions.Accounts.Manage], Ct);
        await AccountJourney.AuthorizeAsync(support, "platform", Ct);
        using var reader = Client(isolated);
        var readerAccount = await AccountJourney.RegisterAsync(isolated, reader, Ct);
        await PlatformOperators.GrantAsync(isolated.Services, readerAccount.UserId, PlatformOperators.Reduced, Ct);
        await AccountJourney.AuthorizeAsync(reader, "platform", Ct);
        using var owner = Client(isolated);
        var ownerAccount = await AccountJourney.RegisterAsync(isolated, owner, Ct);
        var suspendedOrganization = await OrganizationAsync(isolated, "Suspendida", ownerAccount.UserId, TenantStatus.Suspended);
        await OrganizationAsync(isolated, "Cerrada", ownerAccount.UserId, TenantStatus.Closed);

        using var withoutPermission = await PlatformJourney.PostAsync(reader, $"/api/platform/accounts/{ownerAccount.UserId:D}/deletion",
            new { reason = "Sin permiso" }, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, withoutPermission.StatusCode);
        // Una organización suspendida no está cerrada: su único Dueño no se da de baja; la cerrada no cuenta.
        var ownerDetail = await DetailAsync(support, ownerAccount.UserId);
        Assert.Equal(["Suspendida"], ownerDetail.GetProperty("soleOwnerOrganizations").EnumerateArray()
            .Select(item => item.GetString()!));
        using var soleOwner = await PlatformJourney.PostAsync(support, $"/api/platform/accounts/{ownerAccount.UserId:D}/deletion",
            new { reason = "Pedido legal" }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, soleOwner.StatusCode);
        Assert.Equal("Legal.AccountDeletion.LastAdmin", await CodeAsync(soleOwner));

        using var lastManager = await PlatformJourney.PostAsync(support, $"/api/platform/accounts/{managerAccount.UserId:D}/deletion",
            new { reason = "Pedido legal" }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, lastManager.StatusCode);
        Assert.Equal(PlatformOperatorErrors.LastManagerCode, await CodeAsync(lastManager));
        using var self = await PlatformJourney.PostAsync(support, $"/api/platform/accounts/{supportAccount.UserId:D}/deletion",
            new { reason = "Prueba" }, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, self.StatusCode);
        Assert.Equal(UserErrors.CannotModifySelfCode, await CodeAsync(self));

        await using (var connection = new NpgsqlConnection(isolated.AdminConnectionString))
        {
            await connection.OpenAsync(Ct);
            await using var command = new NpgsqlCommand("""UPDATE platform."Tenants" SET "Status" = 'Closed' WHERE "Id" = @id""", connection);
            command.Parameters.AddWithValue("id", suspendedOrganization);
            await command.ExecuteNonQueryAsync(Ct);
        }
        using var allowed = await PlatformJourney.PostAsync(support, $"/api/platform/accounts/{ownerAccount.UserId:D}/deletion",
            new { reason = "Pedido legal" }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
    }

    private static HttpClient Client(ApiFactory api) =>
        api.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private static async Task<JsonElement> DetailAsync(HttpClient client, Guid accountId)
    {
        using var response = await client.GetAsync($"/api/platform/accounts/{accountId:D}", Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement.Clone();
    }

    /// <summary>Carga el correo de soporte en Configuración sin cambiar las demás reglas.</summary>
    private static async Task<string> ConfigureSupportEmailAsync(HttpClient platform)
    {
        using var current = await platform.GetAsync("/api/platform/settings", Ct);
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        var settings = JsonDocument.Parse(await current.Content.ReadAsStringAsync(Ct)).RootElement;
        // Sin números: el lector del pickup toma seis dígitos seguidos como un código.
        const string support = "soporte.plataforma@example.test";
        using var saved = await platform.PutAsJsonAsync("/api/platform/settings", new
        {
            consumerSignup = settings.GetProperty("consumerSignup").GetString(),
            businessSignup = settings.GetProperty("businessSignup").GetString(),
            maxOwnedOrganizations = settings.GetProperty("maxOwnedOrganizations").GetInt32(),
            salesEmail = "ventas@example.test", supportEmail = support,
            version = settings.GetProperty("version").GetUInt32(), reason = "Contacto de soporte",
        }, Ct);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        return support;
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return problem.RootElement.GetProperty("code").GetString();
    }

    private async Task<Guid[]> OutboxIdsAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutboxMessages.AsNoTracking()
            .Where(message => message.UserId == userId).Select(message => message.Id).ToArrayAsync(Ct);
    }

    private static async Task<EmailMessage> NoticeAsync(IServiceProvider services, Guid userId, Guid[] before)
    {
        var message = Assert.Single(await services.GetRequiredService<ApplicationDbContext>().OutboxMessages.AsNoTracking()
            .Where(item => item.UserId == userId && item.Channel == "email" && !before.Contains(item.Id)).ToArrayAsync(Ct));
        return JsonSerializer.Deserialize<EmailMessage>(services.GetRequiredService<IPayloadProtector>()
            .Unprotect(message.EncryptedPayload))!;
    }

    private async Task<string> PendingDeletionTicketAsync(HttpClient client, AccountJourney account)
    {
        await AgeLoginCodesAsync(account.Email.Value);
        client.DefaultRequestHeaders.Authorization = null;
        using var requested = await AccountJourney.PostAsync(client, "/api/auth/login-code", new { email = account.Email.Value }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await PickupCodeReader.ReadAsync(factory.Services, account.Email.Value, Ct);
        using var verified = await AccountJourney.PostAsync(client, "/api/auth/login-code/verify",
            new { email = account.Email.Value, code, returnUrl = "/invitacion" }, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, verified.StatusCode);
        using var problem = await verified.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(AccountErrors.PendingDeletionCode, problem!.RootElement.GetProperty("code").GetString());
        return problem.RootElement.GetProperty("cancelTicket").GetString()!;
    }

    private async Task AgeLoginCodesAsync(string email)
    {
        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("""
            UPDATE identity."LoginCodes" SET "CreatedAtUtc" = "CreatedAtUtc" - interval '1 hour'
            WHERE "Destination" = @email
            """, connection);
        command.Parameters.AddWithValue("email", email);
        await command.ExecuteNonQueryAsync(Ct);
    }

    private static async Task<Guid> OrganizationAsync(ApiFactory isolated, string name, Guid ownerId, TenantStatus status)
    {
        var tenant = Tenant.CreateBusiness(name, false);
        Assert.True(tenant.Activate().IsSuccess);
        if (status != TenantStatus.Active) Assert.True(tenant.Suspend().IsSuccess);
        if (status == TenantStatus.Closed) Assert.True(tenant.Close().IsSuccess);
        await using var scope = isolated.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var entered = services.GetRequiredService<ITenantScope>().Enter(tenant.Id);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            services.GetRequiredService<TenantSpaceProvisioner>().Stage(tenant,
                TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"), [ownerId], ownerId);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        return tenant.Id;
    }
}
