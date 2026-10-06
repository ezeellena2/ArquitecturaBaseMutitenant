using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Organization;

[Collection(ApiTestGroup.Name)]
public sealed class OrganizationAuditTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Audit_returns_affected_person_and_company_for_role_changes()
    {
        using var ownerClient = Client();
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        var tenant = await CreateBusinessAsync("Auditoría ubicación", "auditoria-ubicacion", owner.UserId);
        await AccountJourney.AuthorizeAsync(ownerClient, "business", Ct);

        var target = ApplicationUser.Create("Tomás Acosta", "es-AR", "America/Argentina/Buenos_Aires").Value;
        var userManager = factory.Services.GetRequiredService<UserManager<ApplicationUser>>();
        var created = await userManager.CreateAsync(target);
        Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(error => error.Description)));
        await SeedCompanyRoleAssignmentAsync(tenant.Id, target.Id);

        using var response = await ownerClient.GetAsync("/api/organization/audit?period=all&category=roles&limit=50", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var entry = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray(), item =>
            item.TryGetProperty("affectedDisplayName", out var name) && name.GetString() == "Tomás Acosta");

        Assert.Equal("Cambió los roles de Tomás Acosta", entry.GetProperty("actionText").GetString());
        Assert.Equal("Delta S.A.", entry.GetProperty("whereText").GetString());
        // Tablero Auditoria-Org: «Roles en la empresa: Sin roles → Solo lectura».
        var change = Assert.Single(entry.GetProperty("changes").EnumerateArray());
        Assert.Equal("Roles en la empresa", change.GetProperty("field").GetString());
        Assert.Equal("Sin roles", change.GetProperty("oldValue").GetString());
        Assert.Equal("Solo lectura", change.GetProperty("newValue").GetString());
    }

    [Fact]
    public async Task Changing_the_roles_of_a_person_in_a_company_leaves_one_entry_with_the_roles_before_and_after()
    {
        using var client = Client();
        var company = await StageCompanyMemberAsync(client, "Auditoría roles en empresa");
        var before = await EntryIdsAsync(client);

        // Diálogo «Roles en la empresa» de la ficha de Empresa: quita Ventas y suma Compras en un solo guardado.
        await ChangeCompanyRolesAsync(client, company, company.ReadOnly, company.Purchases);

        // Tablero Auditoria-Org: «Cambió los roles de Tomás Acosta», en Delta S.A., con una sola fila.
        var entry = Assert.Single(await NewEntriesAsync(client, before));
        Assert.Equal("Cambió los roles de Tomás Acosta", entry.GetProperty("actionText").GetString());
        Assert.Equal("Delta S.A.", entry.GetProperty("whereText").GetString());
        var change = Assert.Single(entry.GetProperty("changes").EnumerateArray());
        Assert.Equal("Roles en la empresa", change.GetProperty("field").GetString());
        Assert.Equal("Solo lectura, Ventas", change.GetProperty("oldValue").GetString());
        Assert.Equal("Compras, Solo lectura", change.GetProperty("newValue").GetString());
    }

    [Fact]
    public async Task The_audit_detail_never_shows_an_internal_identifier()
    {
        using var client = Client();
        var company = await StageCompanyMemberAsync(client, "Auditoría sin identificadores");
        await ChangeCompanyRolesAsync(client, company, company.Purchases);
        using var invited = await AccountJourney.PostAsync(client, "/api/users/invitations", new
        {
            email = "valentina.ruiz@example.test",
            roleIds = Array.Empty<Guid>(),
            companies = new[] { new { companyId = company.CompanyId, roleIds = new[] { company.ReadOnly } } },
        }, Ct);
        Assert.True(invited.StatusCode == HttpStatusCode.Created, await invited.Content.ReadAsStringAsync(Ct));

        using var response = await client.GetAsync("/api/organization/audit?period=all&limit=50", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var items = json.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.NotEmpty(items);

        // Quién, qué y dónde van por nombre: el detalle nunca muestra el identificador de una persona, un rol o una empresa.
        var shown = items.SelectMany(item => new[]
            {
                Text(item, "actionText"), Text(item, "affectedDisplayName"), Text(item, "whereText"),
            }.Concat(item.GetProperty("changes").EnumerateArray().SelectMany(change => new[]
            {
                Text(change, "field"), Text(change, "oldValue"), Text(change, "newValue"),
            })))
            .OfType<string>()
            .ToArray();
        Assert.DoesNotContain(shown, InternalId.IsMatch);
        Assert.Contains("Roles en la empresa", shown);
    }

    [Fact]
    public async Task Adding_a_person_to_a_company_with_roles_leaves_one_entry_like_the_board()
    {
        using var client = Client();
        var company = await StageCompanyAsync(client, "Auditoría agregar a empresa");
        var before = await EntryIdsAsync(client);

        await AddToCompanyAsync(client, company, company.ReadOnly);

        // Tablero Auditoria-Org: «Agregó a Paula Méndez», en la empresa, con «Roles en la empresa» adentro.
        var entry = Assert.Single(await NewEntriesAsync(client, before));
        Assert.Equal("Agregó a Tomás Acosta", entry.GetProperty("actionText").GetString());
        Assert.Equal("Delta S.A.", entry.GetProperty("whereText").GetString());
        Assert.Equal(["Roles en la empresa:  → Solo lectura"], Rows(entry));
    }

    [Fact]
    public async Task Creating_a_company_leaves_one_entry_with_its_name_like_the_board()
    {
        using var client = Client();
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        await OrganizationJourney.StageAsync(factory, owner, "Auditoría crear empresa", Ct);
        await AccountJourney.AuthorizeAsync(client, "business", Ct);
        var before = await EntryIdsAsync(client);

        // Quien la crea queda en ella como Administrador: su membresía y su rol van adentro de la misma entrada.
        await OrganizationJourney.CreateCompanyAsync(client, "Delta Norte S.A.", Ct);

        // Tablero Auditoria-Org: «Creó la empresa Delta Norte S.A.», en ella, con solo «Nombre» adentro.
        var entry = Assert.Single(await NewEntriesAsync(client, before));
        Assert.Equal("Creó la empresa Delta Norte S.A.", entry.GetProperty("actionText").GetString());
        Assert.Equal("Delta Norte S.A.", entry.GetProperty("whereText").GetString());
        Assert.Equal(["Nombre:  → Delta Norte S.A."], Rows(entry));
    }

    [Fact]
    public async Task Inviting_a_person_leaves_one_entry_with_the_email_the_company_and_the_roles_like_the_board()
    {
        using var client = Client();
        var company = await StageCompanyMemberAsync(client, "Auditoría invitar");
        var before = await EntryIdsAsync(client);

        await InviteToCompanyAsync(client, company, "valentina.ruiz@example.test");

        // Tablero Auditoria-Org: «Invitó a valentina.ruiz@…» con Correo, Empresa y Roles en la empresa. El correo va
        // enmascarado (auditoria.md).
        var entry = Assert.Single(await NewEntriesAsync(client, before));
        Assert.Equal("Invitó a va•••@example.test", entry.GetProperty("actionText").GetString());
        Assert.Equal("Delta S.A.", entry.GetProperty("whereText").GetString());
        Assert.Equal(["Correo:  → va•••@example.test", "Empresa:  → Delta S.A.", "Roles en la empresa:  → Solo lectura"],
            Rows(entry));
    }

    [Fact]
    public async Task The_roles_of_an_invited_person_appear_with_their_masked_email()
    {
        using var client = Client();
        var company = await StageCompanyMemberAsync(client, "Auditoría roles de un invitado");
        await InviteToCompanyAsync(client, company, "valentina.ruiz@example.test");
        var invited = (await JsonAsync(client, "/api/users/paged?pageSize=50&search=valentina")).GetProperty("items")
            .EnumerateArray().Single();
        var before = await EntryIdsAsync(client);

        using var saved = await client.PutAsJsonAsync($"/api/users/{invited.GetProperty("memberId").GetGuid():D}", new
        {
            roleIds = Array.Empty<Guid>(),
            companies = new[] { new { companyId = company.CompanyId, roleIds = new[] { company.ReadOnly, company.Purchases } } },
            version = invited.GetProperty("version").GetUInt32(),
        }, Ct);
        Assert.True(saved.StatusCode == HttpStatusCode.OK, await saved.Content.ReadAsStringAsync(Ct));

        // Todavía no tiene cuenta ni nombre: se lo nombra por el correo de su invitación, enmascarado.
        var entry = Assert.Single(await NewEntriesAsync(client, before));
        Assert.Equal("Cambió los roles de va•••@example.test", entry.GetProperty("actionText").GetString());
        Assert.Equal(["Roles en la empresa: Solo lectura → Compras, Solo lectura"], Rows(entry));
    }

    [Fact]
    public async Task Removing_a_person_from_a_company_leaves_one_entry_with_the_roles_they_had_there()
    {
        using var client = Client();
        var company = await StageCompanyMemberAsync(client, "Auditoría quitar de empresa");
        var before = await EntryIdsAsync(client);

        // «Quitar de la empresa» de la ficha de Empresa: la membresía queda inactiva y se van sus roles ahí.
        await ChangeCompanyMemberStatusAsync(client, company, "Inactive");

        // Como agregar a alguien: una sola entrada, con «Roles en la empresa» adentro.
        var entry = Assert.Single(await NewEntriesAsync(client, before));
        Assert.Equal("Cambió el acceso de Tomás Acosta", entry.GetProperty("actionText").GetString());
        Assert.Equal("Delta S.A.", entry.GetProperty("whereText").GetString());
        Assert.Equal(["Estado: Activo → Inactivo", "Roles en la empresa: Solo lectura, Ventas → "], Rows(entry));
    }

    [Fact]
    public async Task Revoking_an_invitation_leaves_one_entry()
    {
        using var client = Client();
        var company = await StageCompanyMemberAsync(client, "Auditoría revocar");
        await InviteToCompanyAsync(client, company, "valentina.ruiz@example.test");
        var invited = (await JsonAsync(client, "/api/users/paged?pageSize=50&search=valentina")).GetProperty("items")
            .EnumerateArray().Single();
        var before = await EntryIdsAsync(client);

        // «Revocar invitación» de Usuarios: la persona invitada sale también de la organización y de sus empresas.
        using (var revoked = await client.DeleteAsync(
                   $"/api/users/invitations/{invited.GetProperty("invitationId").GetGuid():D}", Ct))
            Assert.True(revoked.StatusCode == HttpStatusCode.NoContent, await revoked.Content.ReadAsStringAsync(Ct));

        // Como invitar: una sola entrada. El miembro, la membresía y los roles de la persona invitada van adentro.
        var entry = Assert.Single(await NewEntriesAsync(client, before));
        Assert.Equal("Cambió la invitación de va•••@example.test", entry.GetProperty("actionText").GetString());
        Assert.Equal(["Estado: Invitación pendiente → Invitación revocada"], Rows(entry));
    }

    [Fact]
    public async Task Removing_a_person_from_the_organization_leaves_one_entry_with_the_roles_they_had()
    {
        using var client = Client();
        var company = await StageCompanyMemberAsync(client, "Auditoría quitar de la organización");
        var organizationRole = await CreateOrganizationRoleAsync(client, "Recursos humanos");
        var member = await JsonAsync(client, $"/api/users/{company.MemberId:D}");
        using (var saved = await client.PutAsJsonAsync($"/api/users/{company.MemberId:D}", new
               {
                   roleIds = new[] { organizationRole },
                   companies = new[] { new { companyId = company.CompanyId, roleIds = new[] { company.Sales, company.ReadOnly } } },
                   version = member.GetProperty("version").GetUInt32(),
               }, Ct))
            Assert.True(saved.StatusCode == HttpStatusCode.OK, await saved.Content.ReadAsStringAsync(Ct));
        var before = await EntryIdsAsync(client);

        // «Quitar de la organización» de Usuarios: sale también de sus empresas y se van todos sus roles.
        var version = (await JsonAsync(client, $"/api/users/{company.MemberId:D}")).GetProperty("version").GetUInt32();
        using (var removed = await client.DeleteAsync($"/api/users/{company.MemberId:D}?version={version}", Ct))
            Assert.True(removed.StatusCode == HttpStatusCode.NoContent, await removed.Content.ReadAsStringAsync(Ct));

        // Una sola entrada, «Quitó a …», con los roles que tenía, como quitarlo de una empresa.
        var entry = Assert.Single(await NewEntriesAsync(client, before));
        Assert.Equal("Quitó a Tomás Acosta", entry.GetProperty("actionText").GetString());
        Assert.Equal("Toda la organización", entry.GetProperty("whereText").GetString());
        Assert.Equal(["Roles de la organización: Recursos humanos → ", "Roles en la empresa: Solo lectura, Ventas → "],
            Rows(entry));
    }

    [Fact]
    public async Task An_expired_invitation_is_recorded_once_by_the_system_on_its_expiry_date()
    {
        using var client = Client();
        var company = await StageCompanyMemberAsync(client, "Auditoría vencimiento");
        await InviteToCompanyAsync(client, company, "carla.bianchi@example.test");
        var invited = (await JsonAsync(client, "/api/users/paged?pageSize=50&search=carla")).GetProperty("items")
            .EnumerateArray().Single();
        var expiredAtUtc = await ExpireInvitationAsync(company.TenantId, invited.GetProperty("invitationId").GetGuid());
        var before = await EntryIdsAsync(client);

        // El proceso de fondo corre más de una vez: el vencimiento queda registrado una sola.
        var worker = ActivatorUtilities.CreateInstance<InvitationExpiryWorker>(factory.Services);
        await worker.RunOnceAsync(Ct);
        await worker.RunOnceAsync(Ct);

        // Tablero Auditoria-Org: «Venció la invitación a …», en su empresa, por el Sistema y con la fecha en que venció.
        var entry = Assert.Single(await NewEntriesAsync(client, before));
        Assert.Equal("Venció la invitación a ca•••@example.test", entry.GetProperty("actionText").GetString());
        Assert.Equal("Delta S.A.", entry.GetProperty("whereText").GetString());
        Assert.Equal("System", entry.GetProperty("actorKind").GetString());
        Assert.Equal("Sistema", entry.GetProperty("actorText").GetString());
        Assert.Equal(expiredAtUtc, entry.GetProperty("occurredAtUtc").GetDateTime().ToUniversalTime());
        Assert.Equal(["Estado: Invitación pendiente → Invitación vencida"], Rows(entry));
    }

    /// <summary>Deja la invitación vencida hace dos días, en segundos enteros, como si hubiera pasado el tiempo.</summary>
    private async Task<DateTime> ExpireInvitationAsync(Guid tenantId, Guid invitationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var expiredAtUtc = new DateTime(nowUtc.Ticks - nowUtc.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc)
            .AddDays(-2);
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        Assert.Equal(1, await services.GetRequiredService<ApplicationDbContext>().Invitations
            .Where(invitation => invitation.Id == invitationId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(invitation => invitation.ExpiresAtUtc, expiredAtUtc), Ct));
        return expiredAtUtc;
    }

    [Fact]
    public async Task The_platform_approval_appears_without_who_approved_it_or_why()
    {
        using var operatorClient = Client();
        using var ownerClient = Client();
        const string reason = "Motivo interno de la aprobación";
        var (tenant, operatorAccount) = await StageApprovedOrganizationAsync(operatorClient, ownerClient,
            "Auditoría aprobación", reason);
        // Otro cambio hecho por un operador adentro de la organización, como moderar su página pública.
        await SeedPlatformEntryAsync(tenant.Id, operatorAccount.UserId);

        // Tablero Auditoria-Org: «La plataforma aprobó la organización», en toda la organización, por Plataforma.
        using var response = await ownerClient.GetAsync("/api/organization/audit?period=all&category=organization&limit=50", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync(Ct);
        using var json = JsonDocument.Parse(raw);
        var entry = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("actionText").GetString() == "La plataforma aprobó la organización");
        Assert.Equal("Toda la organización", entry.GetProperty("whereText").GetString());
        Assert.Equal("PlatformOperator", entry.GetProperty("actorKind").GetString());
        Assert.Equal("Plataforma", entry.GetProperty("actorText").GetString());
        Assert.Equal(["Estado: Espera aprobación → Activa"], Rows(entry));

        // Nada interno de Plataforma: ni qué operador actuó ni el motivo.
        Assert.DoesNotContain(operatorAccount.UserId.ToString(), raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(reason, raw, StringComparison.Ordinal);
        using var actors = await ownerClient.GetAsync("/api/organization/audit/actors?period=all", Ct);
        Assert.DoesNotContain(operatorAccount.UserId.ToString(), await actors.Content.ReadAsStringAsync(Ct),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Who_offers_the_system_and_the_platform_without_internal_ids_and_filters_by_them()
    {
        using var operatorClient = Client();
        using var ownerClient = Client();
        var (tenant, operatorAccount) = await StageApprovedOrganizationAsync(operatorClient, ownerClient,
            "Auditoría quién actúa", "Motivo interno");
        await OrganizationJourney.CreateCompanyAsync(ownerClient, "Delta S.A.", Ct);
        await OrganizationJourney.InviteExpiredAsync(factory, ownerClient, tenant.Id, "carla.bianchi@example.test", Ct);
        await ActivatorUtilities.CreateInstance<InvitationExpiryWorker>(factory.Services).RunOnceAsync(Ct);

        // Tablero Auditoria-Org: «Quién» ofrece las personas, «Sistema» y «Plataforma», en ese orden.
        var actorsJson = await JsonAsync(ownerClient, "/api/organization/audit/actors?period=all");
        Assert.DoesNotContain(operatorAccount.UserId.ToString(), actorsJson.GetRawText(), StringComparison.OrdinalIgnoreCase);
        var actors = actorsJson.EnumerateArray().ToArray();
        Assert.Equal(["User", "System", "PlatformOperator"],
            actors.Select(actor => actor.GetProperty("actorKind").GetString()).Distinct());
        var system = Assert.Single(actors, actor => actor.GetProperty("actorKind").GetString() == "System");
        Assert.Equal("Sistema", system.GetProperty("actorText").GetString());
        Assert.Null(Text(system, "actorId"));
        var platform = Assert.Single(actors, actor => actor.GetProperty("actorKind").GetString() == "PlatformOperator");
        Assert.Equal("Plataforma", platform.GetProperty("actorText").GetString());
        Assert.Null(Text(platform, "actorId"));

        // Elegir «Sistema» o «Plataforma» deja solo sus entradas; la aprobación, sin el operador.
        var bySystem = await ItemsAsync(ownerClient, "System");
        Assert.All(bySystem, item => Assert.Equal("System", item.GetProperty("actorKind").GetString()));
        Assert.Contains(bySystem, item => item.GetProperty("actionText").GetString() == "Venció la invitación a ca•••@example.test");
        var byPlatform = await ItemsAsync(ownerClient, "PlatformOperator");
        Assert.All(byPlatform, item => Assert.Equal("PlatformOperator", item.GetProperty("actorKind").GetString()));
        Assert.All(byPlatform, item => Assert.Null(Text(item, "actorId")));
        Assert.Contains(byPlatform, item => item.GetProperty("actionText").GetString() == "La plataforma aprobó la organización");
        var byPeople = await ItemsAsync(ownerClient, "User");
        Assert.All(byPeople, item => Assert.Equal("User", item.GetProperty("actorKind").GetString()));
        Assert.Contains(byPeople, item => item.GetProperty("actionText").GetString() == "Creó la empresa Delta S.A.");

        using var invalid = await ownerClient.GetAsync("/api/organization/audit?period=all&actorKind=Operator", Ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var problem = JsonDocument.Parse(await invalid.Content.ReadAsStringAsync(Ct));
        Assert.Equal("Validation.Failed", problem.RootElement.GetProperty("code").GetString());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("actorKind", out _));
    }

    private static async Task<JsonElement[]> ItemsAsync(HttpClient client, string actorKind) =>
    [
        .. (await JsonAsync(client, $"/api/organization/audit?period=all&actorKind={actorKind}&limit=50"))
            .GetProperty("items").EnumerateArray(),
    ];

    /// <summary>
    /// Una organización que esperaba aprobación y que aprobó un operador de Plataforma, con su motivo interno; su Dueño
    /// queda con el acceso de empresa.
    /// </summary>
    private async Task<(Tenant Tenant, AccountJourney Operator)> StageApprovedOrganizationAsync(
        HttpClient operatorClient, HttpClient ownerClient, string name, string reason)
    {
        var operatorAccount = await AccountJourney.RegisterAsync(factory, operatorClient, Ct);
        await PlatformOperators.GrantAsync(factory.Services, operatorAccount.UserId, PlatformOperators.All, Ct);
        await AccountJourney.AuthorizeAsync(operatorClient, "platform", Ct);
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        var tenant = Tenant.CreateBusiness(name, requiresApproval: true);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenant.Id);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
                services.GetRequiredService<TenantProvisioner>().StageAsync(tenant,
                    TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"), [owner.UserId], owner.UserId,
                    ct), CommitPolicy.OnSuccess, Ct);
        }

        var version = (await JsonAsync(operatorClient, $"/api/platform/tenants/{tenant.Id:D}")).GetProperty("version")
            .GetUInt32();
        using (var approved = await PlatformJourney.PostAsync(operatorClient,
                   $"/api/platform/tenants/{tenant.Id:D}/approve", new { version, reason }, Ct))
            Assert.True(approved.StatusCode == HttpStatusCode.OK, await approved.Content.ReadAsStringAsync(Ct));
        await AccountJourney.AuthorizeAsync(ownerClient, "business", Ct);
        return (tenant, operatorAccount);
    }

    private async Task SeedPlatformEntryAsync(Guid tenantId, Guid operatorId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            services.GetRequiredService<ApplicationDbContext>().Add(AuditEntry.Create(tenantId,
                AuditActorKind.PlatformOperator, operatorId, AuditAction.Updated, "PublicPage", Guid.CreateVersion7(),
                "{}", services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
    }

    [Fact]
    public async Task Someone_without_a_name_or_an_invitation_appears_with_their_masked_email()
    {
        using var client = Client();
        var company = await StageCompanyAsync(client, "Auditoría sin nombre");
        // Una cuenta que todavía no puso su nombre, en la organización sin invitación (como quien la registró).
        using var unnamedClient = Client();
        var unnamed = await AccountJourney.RegisterAsync(factory, unnamedClient, Ct,
            Email.Create($"nadia-{Guid.NewGuid():N}@example.test").Value);
        var memberId = await OrganizationJourney.AddActiveMemberAsync(factory, company.TenantId, unnamed.UserId, Ct);
        var before = await EntryIdsAsync(client);

        await AddToCompanyAsync(client, company with { MemberId = memberId }, company.Sales);

        // Tablero Auditoria-Org: «Agregó a …», con lo que haya: su nombre o su correo, enmascarado.
        var added = Assert.Single(await NewEntriesAsync(client, before));
        Assert.Equal("Agregó a na•••@example.test", added.GetProperty("actionText").GetString());
        Assert.Equal("na•••@example.test", added.GetProperty("affectedDisplayName").GetString());
        Assert.Equal("Delta S.A.", added.GetProperty("whereText").GetString());
        Assert.Equal(["Roles en la empresa:  → Ventas"], Rows(added));
    }

    [Fact]
    public async Task Each_detail_row_shows_its_field_by_name_and_its_value_in_the_viewer_language()
    {
        using var client = Client();
        var company = await StageCompanyMemberAsync(client, "Auditoría valores");
        await InviteToCompanyAsync(client, company, "valentina.ruiz@example.test");
        var member = await JsonAsync(client, $"/api/users/{company.MemberId:D}");
        using (var disabled = await client.PutAsJsonAsync($"/api/users/{company.MemberId:D}/status",
                   new { isActive = false, version = member.GetProperty("version").GetUInt32() }, Ct))
            Assert.True(disabled.StatusCode == HttpStatusCode.OK, await disabled.Content.ReadAsStringAsync(Ct));
        var settings = await JsonAsync(client, "/api/organization/settings");
        using (var changed = await client.PutAsJsonAsync("/api/organization/settings", new
               {
                   name = settings.GetProperty("name").GetString(),
                   defaultCulture = "en-US",
                   defaultTimeZoneId = settings.GetProperty("defaultTimeZoneId").GetString(),
                   defaultCurrency = settings.GetProperty("defaultCurrency").GetString(),
                   version = settings.GetProperty("version").GetUInt32(),
               }, Ct))
            Assert.True(changed.StatusCode == HttpStatusCode.OK, await changed.Content.ReadAsStringAsync(Ct));
        // Dos campos en un guardado: el resumen general.
        settings = await JsonAsync(client, "/api/organization/settings");
        using (var both = await client.PutAsJsonAsync("/api/organization/settings", new
               {
                   name = settings.GetProperty("name").GetString(),
                   defaultCulture = "es-AR",
                   defaultTimeZoneId = "America/Argentina/Cordoba",
                   defaultCurrency = settings.GetProperty("defaultCurrency").GetString(),
                   version = settings.GetProperty("version").GetUInt32(),
               }, Ct))
            Assert.True(both.StatusCode == HttpStatusCode.OK, await both.Content.ReadAsStringAsync(Ct));
        var delta = await JsonAsync(client, $"/api/companies/{company.CompanyId:D}");
        using (var moved = await client.PutAsJsonAsync($"/api/companies/{company.CompanyId:D}", new
               {
                   name = "Delta S.A.",
                   taxIdNumber = (string?)null,
                   timeZoneId = "America/Argentina/Cordoba",
                   version = delta.GetProperty("version").GetUInt32(),
               }, Ct))
            Assert.True(moved.StatusCode == HttpStatusCode.OK, await moved.Content.ReadAsStringAsync(Ct));

        // Tablero Auditoria-Org: cada fila con su nombre y su valor, nunca «un registro», fechas, canales ni estados
        // en inglés.
        var spanish = await DetailsAsync(client, "es-AR");
        Assert.DoesNotContain(spanish.Values.SelectMany(rows => rows), row => row.StartsWith("un registro:", StringComparison.Ordinal));
        Assert.Equal(["Estado: Activo → Deshabilitado"], spanish["Deshabilitó a Tomás Acosta"]);
        // Un cambio de un solo campo dice cuál, como el tablero: «Cambió el idioma y la región predeterminados» y
        // «Cambió la zona de …». El idioma, en su propio idioma, como el selector de idioma y región.
        Assert.Equal(["Idioma y región: Español (Argentina) → English (United States)"],
            spanish["Cambió el idioma y la región predeterminados"]);
        Assert.Equal(["Zona de negocio: Buenos Aires (GMT−3) → Córdoba (GMT−3)"], spanish["Cambió la zona de Delta S.A."]);
        Assert.Equal(["Idioma y región: English (United States) → Español (Argentina)",
            "Zona horaria: Buenos Aires (GMT−3) → Córdoba (GMT−3)"], spanish["Cambió la configuración de la organización"]);
        Assert.Equal(["Nombre:  → Ventas", "Permisos:  → Ver miembros"], spanish["Creó el rol Ventas"]);
        Assert.Equal(["Nombre:  → Delta S.A."], spanish["Creó la empresa Delta S.A."]);

        var english = await DetailsAsync(client, "en-US");
        Assert.DoesNotContain(english.Values.SelectMany(rows => rows), row => row.StartsWith("a record:", StringComparison.Ordinal));
        Assert.Equal(["Status: Active → Disabled"], english["Disabled Tomás Acosta"]);
        Assert.Equal(["Language and region: Español (Argentina) → English (United States)"],
            english["Changed the default language and region"]);
        Assert.Equal(["Business time zone: Buenos Aires (GMT−3) → Córdoba (GMT−3)"],
            english["Changed the time zone of Delta S.A."]);
        Assert.Equal(["Language and region: English (United States) → Español (Argentina)",
            "Time zone: Buenos Aires (GMT−3) → Córdoba (GMT−3)"], english["Changed organization settings"]);
        Assert.Equal(["Email:  → va•••@example.test", "Company:  → Delta S.A.", "Roles in the company:  → Solo lectura"],
            english["Invited va•••@example.test"]);
    }

    private static readonly Regex InternalId = new(
        "[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}|[0-9a-f]{32}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>Una empresa con tres roles propios y Tomás Acosta en ella con Ventas y Solo lectura.</summary>
    private async Task<AuditedCompany> StageCompanyMemberAsync(HttpClient client, string organizationName)
    {
        var company = await StageCompanyAsync(client, organizationName);
        await AddToCompanyAsync(client, company, company.Sales, company.ReadOnly);
        return company;
    }

    /// <summary>Una empresa con tres roles propios y Tomás Acosta en la organización, todavía fuera de la empresa.</summary>
    private async Task<AuditedCompany> StageCompanyAsync(HttpClient client, string organizationName)
    {
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, owner, organizationName, Ct);
        await AccountJourney.AuthorizeAsync(client, "business", Ct);
        var companyId = await OrganizationJourney.CreateCompanyAsync(client, "Delta S.A.", Ct);
        var sales = await CreateCompanyRoleAsync(client, "Ventas", companyId);
        var readOnly = await CreateCompanyRoleAsync(client, "Solo lectura", companyId);
        var purchases = await CreateCompanyRoleAsync(client, "Compras", companyId);
        var memberId = await OrganizationJourney.AddNamedMemberAsync(factory, tenantId, "Tomás Acosta", Ct);
        return new AuditedCompany(tenantId, companyId, memberId, sales, readOnly, purchases);
    }

    private static async Task AddToCompanyAsync(HttpClient client, AuditedCompany company, params Guid[] roleIds)
    {
        using var added = await AccountJourney.PostAsync(client, $"/api/companies/{company.CompanyId:D}/members",
            new { memberId = company.MemberId, roleIds }, Ct);
        Assert.True(added.StatusCode == HttpStatusCode.NoContent, await added.Content.ReadAsStringAsync(Ct));
    }

    private static async Task InviteToCompanyAsync(HttpClient client, AuditedCompany company, string email)
    {
        using var invited = await AccountJourney.PostAsync(client, "/api/users/invitations", new
        {
            email,
            roleIds = Array.Empty<Guid>(),
            companies = new[] { new { companyId = company.CompanyId, roleIds = new[] { company.ReadOnly } } },
        }, Ct);
        Assert.True(invited.StatusCode == HttpStatusCode.Created, await invited.Content.ReadAsStringAsync(Ct));
    }

    /// <summary>Las filas del detalle de cada entrada, por su «Qué», en el idioma pedido.</summary>
    private static async Task<Dictionary<string, string[]>> DetailsAsync(
        HttpClient client, string culture)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/organization/audit?period=all&limit=100");
        request.Headers.AcceptLanguage.ParseAdd(culture);
        using var response = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return json.RootElement.GetProperty("items").EnumerateArray()
            .GroupBy(item => item.GetProperty("actionText").GetString()!)
            .ToDictionary(group => group.Key, group => Rows(group.First()));
    }

    /// <summary>«Campo: antes → después», como el detalle del tablero; un lado vacío queda en blanco.</summary>
    private static string[] Rows(JsonElement entry) =>
    [
        .. entry.GetProperty("changes").EnumerateArray()
            .Select(change => $"{Text(change, "field")}: {Text(change, "oldValue")} → {Text(change, "newValue")}"),
    ];

    private static async Task<JsonElement> JsonAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path, Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return json.RootElement.Clone();
    }

    private static async Task<Guid> CreateCompanyRoleAsync(HttpClient client, string name, Guid companyId)
    {
        using var created = await AccountJourney.PostAsync(client, "/api/roles", new
        {
            name,
            description = (string?)null,
            scope = nameof(RoleScope.SpecificCompany),
            companyId,
            permissions = new[] { Permissions.Company.Members.Read },
        }, Ct);
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync(Ct));
        return await created.Content.ReadFromJsonAsync<Guid>(Ct);
    }

    private static async Task<Guid> CreateOrganizationRoleAsync(HttpClient client, string name)
    {
        using var created = await AccountJourney.PostAsync(client, "/api/roles", new
        {
            name,
            description = (string?)null,
            scope = nameof(RoleScope.Organization),
            companyId = (Guid?)null,
            permissions = new[] { Permissions.Users.Read },
        }, Ct);
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync(Ct));
        return await created.Content.ReadFromJsonAsync<Guid>(Ct);
    }

    private static async Task ChangeCompanyRolesAsync(HttpClient client, AuditedCompany company, params Guid[] roleIds)
    {
        using var members = await client.GetAsync($"/api/companies/{company.CompanyId:D}/members/paged?pageSize=50", Ct);
        using var json = JsonDocument.Parse(await members.Content.ReadAsStringAsync(Ct));
        var version = json.RootElement.GetProperty("items").EnumerateArray()
            .Single(row => row.GetProperty("memberId").GetGuid() == company.MemberId)
            .GetProperty("version").GetUInt32();
        using var saved = await client.PutAsJsonAsync(
            $"/api/companies/{company.CompanyId:D}/members/{company.MemberId:D}/roles", new { roleIds, version }, Ct);
        Assert.True(saved.StatusCode == HttpStatusCode.OK, await saved.Content.ReadAsStringAsync(Ct));
    }

    private static async Task ChangeCompanyMemberStatusAsync(HttpClient client, AuditedCompany company, string status)
    {
        var version = (await JsonAsync(client, $"/api/companies/{company.CompanyId:D}/members/paged?pageSize=50"))
            .GetProperty("items").EnumerateArray()
            .Single(row => row.GetProperty("memberId").GetGuid() == company.MemberId)
            .GetProperty("version").GetUInt32();
        using var saved = await client.PutAsJsonAsync(
            $"/api/companies/{company.CompanyId:D}/members/{company.MemberId:D}/status", new { status, version }, Ct);
        Assert.True(saved.StatusCode == HttpStatusCode.OK, await saved.Content.ReadAsStringAsync(Ct));
    }

    private static async Task<HashSet<Guid>> EntryIdsAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/organization/audit?period=all&limit=50", Ct);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return [.. json.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())];
    }

    private static async Task<JsonElement[]> NewEntriesAsync(HttpClient client, HashSet<Guid> before)
    {
        using var response = await client.GetAsync("/api/organization/audit?period=all&limit=50", Ct);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return [.. json.RootElement.GetProperty("items").EnumerateArray()
            .Where(item => !before.Contains(item.GetProperty("id").GetGuid()))
            .Select(item => item.Clone())];
    }

    private sealed record AuditedCompany(Guid TenantId, Guid CompanyId, Guid MemberId, Guid Sales, Guid ReadOnly,
        Guid Purchases);

    [Fact]
    public async Task Audit_detail_shows_who_acted_with_name_and_email()
    {
        using var ownerClient = Client();
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        await CreateBusinessAsync("Auditoría quién", "auditoria-quien", owner.UserId);
        await AccountJourney.AuthorizeAsync(ownerClient, "business", Ct);
        using var created = await AccountJourney.PostAsync(ownerClient, "/api/companies", new
        {
            name = "Delta Norte S.A.",
            taxIdNumber = (string?)null,
            timeZoneId = "America/Argentina/Buenos_Aires",
        }, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var response = await ownerClient.GetAsync("/api/organization/audit?period=all&category=companies&limit=50", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var entry = json.RootElement.GetProperty("items").EnumerateArray()
            .First(item => item.GetProperty("actorKind").GetString() == "User");

        // Tablero Auditoria-Org, detalle: «Quién: Lucía Fernández · lucia.fernandez@delta.ejemplo.com».
        Assert.Equal(owner.Email.Value, entry.GetProperty("actorEmail").GetString());
    }

    [Fact]
    public async Task Someone_who_is_no_longer_a_member_appears_only_with_their_name()
    {
        using var ownerClient = Client();
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        var tenant = await CreateBusinessAsync("Auditoría ex miembros", "auditoria-ex-miembros", owner.UserId);
        await AccountJourney.AuthorizeAsync(ownerClient, "business", Ct);
        var former = ApplicationUser.Create("Tomás Acosta", "es-AR", "America/Argentina/Buenos_Aires").Value;
        var created = await factory.Services.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(former);
        Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(error => error.Description)));
        var formerEmail = $"tomas-{Guid.NewGuid():N}@example.test";
        var memberId = await SeedActingMemberAsync(tenant.Id, former.Id, formerEmail);

        var whileMember = await ActorEntryAsync(ownerClient, former.Id);
        Assert.Equal(formerEmail, whileMember.GetProperty("actorEmail").GetString());

        await RemoveMemberAsync(tenant.Id, memberId);

        // D7: quien ya no es miembro se muestra solo con su nombre, sin el correo.
        var afterLeaving = await ActorEntryAsync(ownerClient, former.Id);
        Assert.Equal("Tomás Acosta", afterLeaving.GetProperty("actorDisplayName").GetString());
        Assert.True(!afterLeaving.TryGetProperty("actorEmail", out var email) || email.ValueKind == JsonValueKind.Null);
        Assert.DoesNotContain(formerEmail, afterLeaving.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<JsonElement> ActorEntryAsync(HttpClient client, Guid actorId)
    {
        using var response = await client.GetAsync($"/api/organization/audit?period=all&actorId={actorId:D}&limit=50", Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return Assert.Single(json.RootElement.GetProperty("items").EnumerateArray()).Clone();
    }

    /// <summary>Suma a la cuenta como miembro activo con un correo verificado y deja una acción suya en la auditoría.</summary>
    private async Task<Guid> SeedActingMemberAsync(Guid tenantId, Guid userId, string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var member = Member.Invite(userId);
        Assert.True(member.Activate(nowUtc).IsSuccess);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            var method = LoginMethod.CreateEmail(userId, Email.Create(email).Value);
            Assert.True(method.Verify(nowUtc).IsSuccess);
            context.LoginMethods.Add(method);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            context.Members.Add(member);
            context.Add(AuditEntry.Create(tenantId, AuditActorKind.User, userId, AuditAction.Created, "Company",
                Guid.CreateVersion7(), "{}", nowUtc));
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        return member.Id;
    }

    private async Task RemoveMemberAsync(Guid tenantId, Guid memberId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var member = await services.GetRequiredService<ApplicationDbContext>().Members
                .SingleAsync(row => row.Id == memberId, ct);
            Assert.True(member.Remove().IsSuccess);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
    }

    [Fact]
    public async Task Audit_is_tenant_scoped_searchable_paginated_and_masks_personal_data()
    {
        using var ownerClient = Client();
        using var otherClient = Client();
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        var other = await AccountJourney.RegisterAsync(factory, otherClient, Ct);
        var tenant = await CreateBusinessAsync("Auditoría test", "auditoria-test", owner.UserId);
        var otherTenant = await CreateBusinessAsync("Otra auditoría test", "otra-auditoria", other.UserId);
        await AccountJourney.AuthorizeAsync(ownerClient, "business", Ct);
        await AccountJourney.AuthorizeAsync(otherClient, "business", Ct);
        await SeedEntriesAsync(tenant.Id, 49);
        await SeedEntriesAsync(otherTenant.Id, 1);

        using var firstPage = await ownerClient.GetAsync("/api/organization/audit?period=all&category=users&limit=10", Ct);
        Assert.Equal(HttpStatusCode.OK, firstPage.StatusCode);
        using var firstJson = JsonDocument.Parse(await firstPage.Content.ReadAsStringAsync(Ct));
        var first = firstJson.RootElement;
        Assert.Equal(10, first.GetProperty("items").GetArrayLength());
        Assert.True(first.GetProperty("hasMore").GetBoolean());
        var cursor = first.GetProperty("nextCursor").GetString();
        Assert.False(string.IsNullOrWhiteSpace(cursor));

        using var secondPage = await ownerClient.GetAsync(
            $"/api/organization/audit?period=all&category=users&limit=50&after={Uri.EscapeDataString(cursor!)}", Ct);
        Assert.Equal(HttpStatusCode.OK, secondPage.StatusCode);
        using var secondJson = JsonDocument.Parse(await secondPage.Content.ReadAsStringAsync(Ct));
        var second = secondJson.RootElement;
        Assert.Equal(40, second.GetProperty("items").GetArrayLength());
        Assert.False(second.GetProperty("hasMore").GetBoolean());
        var ids = first.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())
            .Concat(second.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()))
            .ToArray();
        Assert.Equal(50, ids.Distinct().Count());

        using var search = await ownerClient.GetAsync(
            "/api/organization/audit?period=all&category=users&search=private-004%40example.test", Ct);
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        using var searchJson = JsonDocument.Parse(await search.Content.ReadAsStringAsync(Ct));
        var result = searchJson.RootElement.GetProperty("items");
        var match = Assert.Single(result.EnumerateArray());
        Assert.DoesNotContain("private-004@example.test", match.GetRawText(), StringComparison.OrdinalIgnoreCase);
        var values = match.GetProperty("changes").EnumerateArray()
            .Select(change => change.GetProperty("newValue").GetString()).ToArray();
        Assert.Contains("pr•••@example.test", values);
        Assert.Contains("•••• 8900", values);

        using var actors = await ownerClient.GetAsync("/api/organization/audit/actors?period=all&category=users", Ct);
        Assert.Equal(HttpStatusCode.OK, actors.StatusCode);
        using var actorsJson = JsonDocument.Parse(await actors.Content.ReadAsStringAsync(Ct));
        Assert.NotEmpty(actorsJson.RootElement.EnumerateArray());
        Assert.DoesNotContain("private-004@example.test", actorsJson.RootElement.GetRawText(),
            StringComparison.OrdinalIgnoreCase);

        using var unauthorized = Client();
        using var denied = await unauthorized.GetAsync("/api/organization/audit", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }

    private HttpClient Client() => factory.CreateClient(new()
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
    });

    private async Task<Tenant> CreateBusinessAsync(string name, string slug, Guid userId)
    {
        var tenant = Tenant.CreateBusiness(name, requiresApproval: false);
        Assert.True(tenant.Activate().IsSuccess);
        Assert.True(tenant.SetSlug(TenantSlug.Create(slug).Value).IsSuccess);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenant.Id);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
        {
            services.GetRequiredService<TenantSpaceProvisioner>().Stage(tenant,
                TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"), [userId], userId);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        return tenant;
    }

    private async Task SeedEntriesAsync(Guid tenantId, int count)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
        {
            var audit = services.GetRequiredService<IAuditLog>();
            for (var index = 0; index < count; index++)
            {
                var email = $"private-{index:000}@example.test";
                audit.Record(AuditAction.Custom, "Member", Guid.NewGuid(), new Dictionary<string, object?>
                {
                    ["Email"] = new Dictionary<string, object?> { ["old"] = null, ["new"] = email },
                    ["Phone"] = new Dictionary<string, object?> { ["old"] = null, ["new"] = "+541145678900" },
                });
            }

            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
    }

    private async Task SeedCompanyRoleAssignmentAsync(Guid tenantId, Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        var context = services.GetRequiredService<ApplicationDbContext>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var member = Member.Invite(userId);
        Assert.True(member.Activate(nowUtc).IsSuccess);
        var company = Company.Create("Delta S.A.", null, "America/Argentina/Buenos_Aires").Value;
        var role = Role.Create("Solo lectura", null, RoleScope.AnyCompany, null,
            [Permissions.Company.Members.Read]).Value;
        await unitOfWork.ExecuteInTransactionAsync(_ =>
        {
            context.AddRange(member, company, role, CompanyMembership.Create(company.Id, member.Id).Value);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        // En otro guardado, para que sea un cambio de roles y no el alta en la empresa (que los lleva adentro).
        await unitOfWork.ExecuteInTransactionAsync(_ =>
        {
            context.Add(RoleAssignment.Assign(member.Id, role.Id, role.Scope, role.CompanyId, company.Id).Value);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
    }
}
