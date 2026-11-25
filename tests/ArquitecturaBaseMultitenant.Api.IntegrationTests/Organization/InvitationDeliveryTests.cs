using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Organization;

/// <summary>
/// B6 (ADR 0066 §4): cada invitación guarda el mensaje de la cola que la lleva, y Usuarios, Usuario y los miembros de una
/// empresa derivan de la cola su estado de entrega: en la cola, enviada, «Sin confirmar» o no salió; sin fila, enviada.
/// Reenviar sigue al mensaje nuevo, y una organización nunca ve el estado de las invitaciones de otra.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class InvitationDeliveryTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_email_invitation_shows_each_queue_state_in_users_the_user_and_its_company()
    {
        using var client = Client();
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, owner, "Entregas por correo", Ct);
        await AccountJourney.AuthorizeAsync(client, "business", Ct);
        var companyId = await OrganizationJourney.CreateCompanyAsync(client, "Norte", Ct);
        var invitationId = await InviteAsync(client, $"entrega-{Guid.NewGuid():N}@example.test",
            new[] { new { companyId, roleIds = new[] { await CompanyAdminRoleIdAsync(client, companyId) } } });
        var invitation = await InvitationAsync(tenantId, invitationId);
        var outboxId = Assert.IsType<Guid>(invitation.OutboxMessageId);
        await using (var scope = factory.Services.CreateAsyncScope())
            Assert.Equal("email", (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutboxMessages
                .AsNoTracking().SingleAsync(message => message.Id == outboxId, Ct)).Channel);

        // En Testing nadie despacha: el correo espera en la cola.
        await AssertDeliveryAsync(client, invitation.MemberId, companyId, "Pending");
        foreach (var (status, expected) in new[]
                 {
                     (OutboxStatus.Sent, "Sent"), (OutboxStatus.Uncertain, "Uncertain"), (OutboxStatus.Failed, "Failed"),
                     (OutboxStatus.Cancelled, "Failed"), (OutboxStatus.Pending, "Pending"),
                 })
        {
            await OutboxRows.SetStatusAsync(factory.Services, outboxId, status, Ct);
            await AssertDeliveryAsync(client, invitation.MemberId, companyId, expected);
        }

        // Sin fila en la cola (purgada), enviada.
        await OutboxRows.DeleteAsync(factory, outboxId, Ct);
        await AssertDeliveryAsync(client, invitation.MemberId, companyId, "Sent");

        // Quien no tiene invitación pendiente no tiene estado de entrega.
        var ownerRow = Assert.Single((await UsersAsync(client)), row => row.GetProperty("userId").ValueKind == JsonValueKind.String
            && row.GetProperty("userId").GetGuid() == owner.UserId);
        Assert.Equal(JsonValueKind.Null, ownerRow.GetProperty("invitationDelivery").ValueKind);
    }

    [Fact]
    public async Task Resending_follows_the_new_message_and_the_old_state_does_not_stay()
    {
        using var client = Client();
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, owner, "Entregas reenviadas", Ct);
        await AccountJourney.AuthorizeAsync(client, "business", Ct);
        var first = await InvitationAsync(tenantId, await InviteAsync(client, $"reenvio-{Guid.NewGuid():N}@example.test"));
        await OutboxRows.SetStatusAsync(factory.Services, first.OutboxMessageId!.Value, OutboxStatus.Uncertain, Ct);
        Assert.Equal("Uncertain", (await UserAsync(client, first.MemberId)).GetProperty("invitationDelivery").GetString());

        using var resent = await AccountJourney.PostAsync(client, $"/api/users/invitations/{first.Id:D}/resend", new { }, Ct);
        Assert.True(resent.StatusCode == HttpStatusCode.OK, await resent.Content.ReadAsStringAsync(Ct));
        var replacement = await InvitationAsync(tenantId, JsonDocument.Parse(await resent.Content.ReadAsStringAsync(Ct)).RootElement.GetGuid());

        Assert.Equal(first.MemberId, replacement.MemberId);
        Assert.NotNull(replacement.OutboxMessageId);
        Assert.NotEqual(first.OutboxMessageId, replacement.OutboxMessageId);
        // La revocada conserva su mensaje: no se reasigna a la nueva.
        Assert.Equal(first.OutboxMessageId, (await InvitationAsync(tenantId, first.Id)).OutboxMessageId);
        Assert.Equal("Pending", (await UserAsync(client, first.MemberId)).GetProperty("invitationDelivery").GetString());
        await OutboxRows.SetStatusAsync(factory.Services, replacement.OutboxMessageId!.Value, OutboxStatus.Sent, Ct);
        Assert.Equal("Sent", (await UserAsync(client, first.MemberId)).GetProperty("invitationDelivery").GetString());
    }

    [Fact]
    public async Task An_organization_never_sees_the_delivery_of_another_organizations_invitation()
    {
        using var clientA = Client();
        var ownerA = await AccountJourney.RegisterAsync(factory, clientA, Ct);
        var tenantA = await OrganizationJourney.StageAsync(factory, ownerA, "Entregas A", Ct);
        await AccountJourney.AuthorizeAsync(clientA, "business", Ct);
        using var clientB = Client();
        var ownerB = await AccountJourney.RegisterAsync(factory, clientB, Ct);
        var tenantB = await OrganizationJourney.StageAsync(factory, ownerB, "Entregas B", Ct);
        await AccountJourney.AuthorizeAsync(clientB, "business", Ct);
        // El mismo correo invitado en las dos: cada una ve solo su invitación, con el estado de su propio mensaje.
        var email = $"ambas-{Guid.NewGuid():N}@example.test";
        var invitationA = await InvitationAsync(tenantA, await InviteAsync(clientA, email));
        var invitationB = await InvitationAsync(tenantB, await InviteAsync(clientB, email));
        Assert.NotEqual(invitationA.OutboxMessageId, invitationB.OutboxMessageId);
        await OutboxRows.SetStatusAsync(factory.Services, invitationA.OutboxMessageId!.Value, OutboxStatus.Failed, Ct);
        await OutboxRows.SetStatusAsync(factory.Services, invitationB.OutboxMessageId!.Value, OutboxStatus.Uncertain, Ct);

        var rowA = Assert.Single(await UsersAsync(clientA), row => row.GetProperty("memberId").GetGuid() == invitationA.MemberId);
        Assert.Equal("Failed", rowA.GetProperty("invitationDelivery").GetString());
        var rowB = Assert.Single(await UsersAsync(clientB), row => row.GetProperty("memberId").GetGuid() == invitationB.MemberId);
        Assert.Equal("Uncertain", rowB.GetProperty("invitationDelivery").GetString());
        Assert.DoesNotContain(await UsersAsync(clientA), row => row.GetProperty("memberId").GetGuid() == invitationB.MemberId);
        Assert.Equal("Failed", (await UserAsync(clientA, invitationA.MemberId)).GetProperty("invitationDelivery").GetString());
        using var foreign = await clientA.GetAsync($"/api/users/{invitationB.MemberId:D}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.DoesNotContain("Uncertain", await foreign.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    private HttpClient Client() => factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private static async Task<Guid> InviteAsync(HttpClient client, string email, object[]? companies = null)
    {
        using var issued = await AccountJourney.PostAsync(client, "/api/users/invitations", new
        {
            email,
            roleIds = Array.Empty<Guid>(),
            companies = companies ?? [],
        }, Ct);
        Assert.True(issued.StatusCode == HttpStatusCode.Created, await issued.Content.ReadAsStringAsync(Ct));
        using var json = JsonDocument.Parse(await issued.Content.ReadAsStringAsync(Ct));
        return json.RootElement.GetProperty("invitationId").GetGuid();
    }

    private async Task<Invitation> InvitationAsync(Guid tenantId, Guid invitationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Invitations.AsNoTracking()
            .SingleAsync(invitation => invitation.Id == invitationId, Ct);
    }

    private static async Task<Guid> CompanyAdminRoleIdAsync(HttpClient client, Guid companyId)
    {
        using var response = await client.GetAsync($"/api/roles/paged?companyId={companyId:D}&pageSize=50", Ct);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return Assert.Single(json.RootElement.GetProperty("items").EnumerateArray(),
            role => role.GetProperty("isSystemRole").GetBoolean()).GetProperty("id").GetGuid();
    }

    private static async Task AssertDeliveryAsync(HttpClient client, Guid memberId, Guid companyId, string expected)
    {
        var listed = Assert.Single(await UsersAsync(client), row => row.GetProperty("memberId").GetGuid() == memberId);
        Assert.Equal(expected, listed.GetProperty("invitationDelivery").GetString());
        Assert.Equal(expected, (await UserAsync(client, memberId)).GetProperty("invitationDelivery").GetString());
        var member = Assert.Single(await RowsAsync(client, $"/api/companies/{companyId:D}/members/paged?pageSize=50"),
            row => row.GetProperty("memberId").GetGuid() == memberId);
        Assert.Equal(expected, member.GetProperty("invitationDelivery").GetString());
    }

    private static Task<JsonElement[]> UsersAsync(HttpClient client) => RowsAsync(client, "/api/users/paged?pageSize=50");

    private static async Task<JsonElement[]> RowsAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path, Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return [.. json.RootElement.GetProperty("items").EnumerateArray().Select(row => row.Clone())];
    }

    private static async Task<JsonElement> UserAsync(HttpClient client, Guid memberId)
    {
        using var response = await client.GetAsync($"/api/users/{memberId:D}", Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return json.RootElement.Clone();
    }
}
