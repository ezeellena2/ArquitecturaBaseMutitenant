using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Organization;

/// <summary>
/// Lo que hace desde la ficha de su empresa un Administrador de empresa sin roles de la organización (decisión del
/// 2026-10-05): sumar a personas de la organización, que ve con el correo tapado, y reenviar o revocar las
/// invitaciones de su empresa.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class CompanyAdminTests(ApiFactory factory)
{
    private const string InvitationNotFound = "Invitations.Invitation.NotFound";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_company_admin_lists_who_can_join_its_company_by_name_with_a_masked_email()
    {
        using var ownerClient = NewClient();
        using var adminClient = NewClient();
        var staged = await StageAsync(ownerClient, adminClient, "Candidatos de una empresa");
        var luciaId = await OrganizationJourney.AddNamedMemberAsync(factory, staged.TenantId, "Lucía Pérez", Ct);
        using var personClient = NewClient();
        var person = await AccountJourney.RegisterAsync(factory, personClient, Ct);
        var personId = await OrganizationJourney.AddActiveMemberAsync(factory, staged.TenantId, person.UserId, Ct);
        var alreadyInId = await OrganizationJourney.AddNamedMemberAsync(factory, staged.TenantId, "Lucio Norte", Ct);
        using (var added = await AccountJourney.PostAsync(ownerClient, $"/api/companies/{staged.NorthId:D}/members",
                   new { memberId = alreadyInId, roleIds = new[] { staged.CompanyAdminRoleId } }, Ct))
            Assert.True(added.StatusCode == HttpStatusCode.NoContent, await added.Content.ReadAsStringAsync(Ct));
        await InviteAsync(ownerClient, $"candidata-{Guid.NewGuid():N}@example.test", []);
        var invitedId = Assert.Single(Ids(await JsonAsync(ownerClient, "/api/users/paged?state=Invited&pageSize=50")));

        var path = $"/api/companies/{staged.NorthId:D}/candidates/paged?pageSize=50";
        var page = await JsonAsync(adminClient, path);

        // Las personas activas de la organización que no están en la empresa: Lucía y la persona registrada. Ni el
        // Dueño, que la creó y está en ella, ni el Administrador ni quien ya está, ni una invitación pendiente.
        var ids = Ids(page);
        Assert.Equal(2, page.GetProperty("totalCount").GetInt32());
        Assert.Contains(luciaId, ids);
        Assert.Contains(personId, ids);
        Assert.DoesNotContain(staged.AdminMemberId, ids);
        Assert.DoesNotContain(alreadyInId, ids);
        Assert.DoesNotContain(invitedId, ids);

        // El nombre y el correo tapado; nunca el correo completo ni la cuenta.
        var items = page.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal("Lucía Pérez", Assert.Single(items, item => item.GetProperty("memberId").GetGuid() == luciaId)
            .GetProperty("displayName").GetString());
        Assert.Equal("ac•••@example.test", Assert.Single(items, item => item.GetProperty("memberId").GetGuid() == personId)
            .GetProperty("maskedEmailAddress").GetString());
        Assert.All(items, item => Assert.Equal(["memberId", "displayName", "maskedEmailAddress"],
            item.EnumerateObject().Select(property => property.Name)));
        Assert.DoesNotContain(person.Email.Value, page.GetRawText(), StringComparison.OrdinalIgnoreCase);

        // Se busca por nombre, sin acentos ni mayúsculas. El correo no se busca: así no se destapa de a una letra.
        Assert.Equal([luciaId], Ids(await JsonAsync(adminClient, path + "&search=LUCIA%20per")));
        Assert.Empty(Ids(await JsonAsync(adminClient, path + "&search=example.test")));

        // Otra empresa, 403; sin sesión, 401. El Dueño, que ve Usuarios, recibe la misma lista.
        using var otherCompany = await adminClient.GetAsync($"/api/companies/{staged.SouthId:D}/candidates/paged", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, otherCompany.StatusCode);
        using var anonymousClient = NewClient();
        using var anonymous = await anonymousClient.GetAsync(path, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(ids.Order(), Ids(await JsonAsync(ownerClient, path)).Order());
    }

    [Fact]
    public async Task A_company_admin_resends_and_revokes_only_the_invitations_of_its_company()
    {
        using var ownerClient = NewClient();
        using var adminClient = NewClient();
        var staged = await StageAsync(ownerClient, adminClient, "Invitaciones de una empresa");
        var northId = await InviteAsync(ownerClient, $"empresa-{Guid.NewGuid():N}@example.test",
            [new { companyId = staged.NorthId, roleIds = new[] { staged.CompanyAdminRoleId } }]);
        var southId = await InviteAsync(ownerClient, $"otra-{Guid.NewGuid():N}@example.test",
            [new { companyId = staged.SouthId, roleIds = new[] { staged.CompanyAdminRoleId } }]);
        var organizationId = await InviteAsync(ownerClient, $"toda-{Guid.NewGuid():N}@example.test", []);
        var north = $"/api/companies/{staged.NorthId:D}/invitations";
        var south = $"/api/companies/{staged.SouthId:D}/invitations";

        // La ficha de su empresa trae la invitación de la persona invitada, para reenviarla o revocarla.
        var invited = Assert.Single((await JsonAsync(adminClient,
                $"/api/companies/{staged.NorthId:D}/members/paged?state=Invited&pageSize=50"))
            .GetProperty("items").EnumerateArray());
        Assert.Equal(northId, invited.GetProperty("invitationId").GetGuid());
        Assert.Equal(JsonValueKind.String, invited.GetProperty("invitationExpiresAtUtc").ValueKind);

        // Reenviar: una invitación nueva para la misma persona, y la anterior queda revocada, como en Usuarios.
        using var resent = await AccountJourney.PostAsync(adminClient, $"{north}/{northId:D}/resend", new { }, Ct);
        Assert.True(resent.StatusCode == HttpStatusCode.OK, await resent.Content.ReadAsStringAsync(Ct));
        var resentId = await resent.Content.ReadFromJsonAsync<Guid>(Ct);
        Assert.NotEqual(northId, resentId);
        Assert.Equal(InvitationStatus.Revoked, await StatusAsync(staged.TenantId, northId));
        Assert.Equal(InvitationStatus.Pending, await StatusAsync(staged.TenantId, resentId));

        // Las de otra empresa y las de toda la organización no son de su empresa: por su empresa no existen (404) y
        // por la otra empresa no tiene permiso (403). Usuarios sigue pidiendo users.invite.
        foreach (var foreignId in new[] { southId, organizationId })
        {
            using var resendForeign = await AccountJourney.PostAsync(adminClient, $"{north}/{foreignId:D}/resend",
                new { }, Ct);
            Assert.Equal(HttpStatusCode.NotFound, resendForeign.StatusCode);
            Assert.Equal(InvitationNotFound, await ProblemCodeAsync(resendForeign));
            using var revokeForeign = await adminClient.DeleteAsync($"{north}/{foreignId:D}", Ct);
            Assert.Equal(HttpStatusCode.NotFound, revokeForeign.StatusCode);
            Assert.Equal(InvitationNotFound, await ProblemCodeAsync(revokeForeign));
            Assert.Equal(InvitationStatus.Pending, await StatusAsync(staged.TenantId, foreignId));
        }
        using var resendSouth = await AccountJourney.PostAsync(adminClient, $"{south}/{southId:D}/resend", new { }, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, resendSouth.StatusCode);
        using var revokeSouth = await adminClient.DeleteAsync($"{south}/{southId:D}", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, revokeSouth.StatusCode);
        using var fromUsers = await AccountJourney.PostAsync(adminClient, $"/api/users/invitations/{resentId:D}/resend",
            new { }, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, fromUsers.StatusCode);

        // Revocar deja la misma entrada de auditoría que «Revocar invitación» de Usuarios.
        var before = await EntryIdsAsync(ownerClient);
        using var revoked = await adminClient.DeleteAsync($"{north}/{resentId:D}", Ct);
        Assert.True(revoked.StatusCode == HttpStatusCode.NoContent, await revoked.Content.ReadAsStringAsync(Ct));
        Assert.Equal(InvitationStatus.Revoked, await StatusAsync(staged.TenantId, resentId));
        var entry = Assert.Single(await NewEntriesAsync(ownerClient, before));
        Assert.Equal("Cambió la invitación de em•••@example.test", entry.GetProperty("actionText").GetString());
        Assert.Equal(["Estado: Invitación pendiente → Invitación revocada"], Rows(entry));

        // Sin sesión, 401.
        using var anonymousClient = NewClient();
        using var anonymousResend = await AccountJourney.PostAsync(anonymousClient, $"{north}/{southId:D}/resend",
            new { }, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResend.StatusCode);
        using var anonymousRevoke = await anonymousClient.DeleteAsync($"{north}/{southId:D}", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousRevoke.StatusCode);
    }

    // B8 y «Para resolver»: a una invitación vencida le sacan la empresa. Ya no es de esa empresa: no da fila al
    // Administrador, la ficha la sigue mostrando «Invitado» pero sin invitationId, y reenviarla desde la empresa da 404.
    [Fact]
    public async Task An_invitation_that_no_longer_includes_the_company_is_not_one_of_its_invitations()
    {
        using var ownerClient = NewClient();
        using var adminClient = NewClient();
        var staged = await StageAsync(ownerClient, adminClient, "Invitación sin la empresa");
        await OrganizationJourney.InviteExpiredAsync(factory, ownerClient, staged.TenantId,
            $"sin-norte-{Guid.NewGuid():N}@example.test", Ct,
            [new { companyId = staged.NorthId, roleIds = new[] { staged.CompanyAdminRoleId } }]);
        var invited = Assert.Single((await JsonAsync(ownerClient, "/api/users/paged?state=Expired&pageSize=50"))
            .GetProperty("items").EnumerateArray());
        var memberId = invited.GetProperty("memberId").GetGuid();
        var invitationId = invited.GetProperty("invitationId").GetGuid();
        Assert.Single(HomeTests.Pending(await OrganizationJourney.HomeAsync(adminClient, Ct)));

        using (var edited = await ownerClient.PutAsJsonAsync($"/api/users/{memberId:D}", new
               {
                   roleIds = Array.Empty<Guid>(),
                   companies = Array.Empty<object>(),
                   version = invited.GetProperty("version").GetUInt32(),
               }, Ct))
            Assert.True(edited.StatusCode == HttpStatusCode.OK, await edited.Content.ReadAsStringAsync(Ct));

        Assert.Empty(HomeTests.Pending(await OrganizationJourney.HomeAsync(adminClient, Ct)));
        var row = Assert.Single((await JsonAsync(adminClient,
                $"/api/companies/{staged.NorthId:D}/members/paged?state=Invited&pageSize=50"))
            .GetProperty("items").EnumerateArray());
        Assert.Equal(memberId, row.GetProperty("memberId").GetGuid());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("invitationId").ValueKind);
        using var resent = await AccountJourney.PostAsync(adminClient,
            $"/api/companies/{staged.NorthId:D}/invitations/{invitationId:D}/resend", new { }, Ct);
        Assert.Equal(HttpStatusCode.NotFound, resent.StatusCode);
        Assert.Equal(InvitationNotFound, await ProblemCodeAsync(resent));
    }

    // Aislamiento: el Dueño de otra organización no llega a una empresa ni a una invitación de esta.
    [Fact]
    public async Task Another_organization_gets_404_for_the_candidates_and_the_invitations_of_this_one()
    {
        using var ownerClient = NewClient();
        using var adminClient = NewClient();
        var staged = await StageAsync(ownerClient, adminClient, "Empresa de otra organización");
        var invitationId = await InviteAsync(ownerClient, $"ajena-{Guid.NewGuid():N}@example.test",
            [new { companyId = staged.NorthId, roleIds = new[] { staged.CompanyAdminRoleId } }]);
        using var strangerClient = NewClient();
        var stranger = await AccountJourney.RegisterAsync(factory, strangerClient, Ct);
        await OrganizationJourney.StageAsync(factory, stranger, "Otra organización", Ct);
        await AccountJourney.AuthorizeAsync(strangerClient, "business", Ct);
        var ownCompanyId = await OrganizationJourney.CreateCompanyAsync(strangerClient, "Gamma S.A.", Ct);

        using var candidates = await strangerClient.GetAsync($"/api/companies/{staged.NorthId:D}/candidates/paged", Ct);
        Assert.Equal(HttpStatusCode.NotFound, candidates.StatusCode);
        foreach (var companyId in new[] { staged.NorthId, ownCompanyId })
        {
            var invitation = $"/api/companies/{companyId:D}/invitations/{invitationId:D}";
            using var resent = await AccountJourney.PostAsync(strangerClient, invitation + "/resend", new { }, Ct);
            Assert.Equal(HttpStatusCode.NotFound, resent.StatusCode);
            using var revoked = await strangerClient.DeleteAsync(invitation, Ct);
            Assert.Equal(HttpStatusCode.NotFound, revoked.StatusCode);
        }
        Assert.Equal(InvitationStatus.Pending, await StatusAsync(staged.TenantId, invitationId));
    }

    // Una organización con el Dueño, dos empresas y un Administrador de Norte sin roles de la organización.
    private async Task<Staged> StageAsync(HttpClient ownerClient, HttpClient adminClient, string name)
    {
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, owner, name, Ct);
        await AccountJourney.AuthorizeAsync(ownerClient, "business", Ct);
        var northId = await OrganizationJourney.CreateCompanyAsync(ownerClient, "Delta Norte S.A.", Ct);
        var southId = await OrganizationJourney.CreateCompanyAsync(ownerClient, "Delta Sur S.A.", Ct);
        var roles = await JsonAsync(ownerClient, "/api/roles/paged?scope=Company&page=1&pageSize=50");
        var companyAdminRoleId = roles.GetProperty("items").EnumerateArray()
            .Single(role => role.GetProperty("isSystemRole").GetBoolean()).GetProperty("id").GetGuid();

        var admin = await AccountJourney.RegisterAsync(factory, adminClient, Ct);
        var adminMemberId = await OrganizationJourney.AddActiveMemberAsync(factory, tenantId, admin.UserId, Ct);
        using (var added = await AccountJourney.PostAsync(ownerClient, $"/api/companies/{northId:D}/members",
                   new { memberId = adminMemberId, roleIds = new[] { companyAdminRoleId } }, Ct))
            Assert.True(added.StatusCode == HttpStatusCode.NoContent, await added.Content.ReadAsStringAsync(Ct));
        await AccountJourney.AuthorizeAsync(adminClient, "business", Ct);
        return new Staged(tenantId, adminMemberId, northId, southId, companyAdminRoleId);
    }

    private static async Task<Guid> InviteAsync(HttpClient ownerClient, string email, object[] companies)
    {
        using var issued = await AccountJourney.PostAsync(ownerClient, "/api/users/invitations",
            new { email, roleIds = Array.Empty<Guid>(), companies }, Ct);
        Assert.True(issued.StatusCode == HttpStatusCode.Created, await issued.Content.ReadAsStringAsync(Ct));
        using var json = JsonDocument.Parse(await issued.Content.ReadAsStringAsync(Ct));
        return json.RootElement.GetProperty("invitationId").GetGuid();
    }

    private async Task<InvitationStatus> StatusAsync(Guid tenantId, Guid invitationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
        var invitation = await scope.ServiceProvider.GetRequiredService<IInvitationReader>()
            .FindByIdAsync(invitationId, Ct);
        Assert.NotNull(invitation);
        return invitation.Status;
    }

    private static async Task<JsonElement> JsonAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path, Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return json.RootElement.Clone();
    }

    private static Guid[] Ids(JsonElement page) =>
        [.. page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("memberId").GetGuid())];

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return json.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    private static async Task<HashSet<Guid>> EntryIdsAsync(HttpClient client) =>
        [.. (await JsonAsync(client, "/api/organization/audit?period=all&limit=50")).GetProperty("items")
            .EnumerateArray().Select(item => item.GetProperty("id").GetGuid())];

    private static async Task<JsonElement[]> NewEntriesAsync(HttpClient client, HashSet<Guid> before) =>
        [.. (await JsonAsync(client, "/api/organization/audit?period=all&limit=50")).GetProperty("items")
            .EnumerateArray().Where(item => !before.Contains(item.GetProperty("id").GetGuid()))];

    /// <summary>«Campo: antes → después», como el detalle del tablero de Auditoría.</summary>
    private static string[] Rows(JsonElement entry) =>
    [
        .. entry.GetProperty("changes").EnumerateArray().Select(change =>
            $"{change.GetProperty("field").GetString()}: {change.GetProperty("oldValue").GetString()} → "
            + change.GetProperty("newValue").GetString()),
    ];

    private HttpClient NewClient() =>
        factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private sealed record Staged(Guid TenantId, Guid AdminMemberId, Guid NorthId, Guid SouthId,
        Guid CompanyAdminRoleId);
}
