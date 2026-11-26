using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Recorrido;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Organization;

/// <summary>
/// La orden privada <c>delivery</c> del recorrido para una invitación (B-2 de la puerta de la base 2): deja el envío de la
/// cola de la última invitación pendiente de un correo de prueba en cada estado que Usuarios lee (en la cola, «Sin
/// confirmar» y sin salir, con su motivo), solo en la organización que se le da, y rechaza lo que no es de prueba.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class RecorridoInvitationDeliveryTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_order_leaves_the_last_invitation_of_an_email_in_each_delivery_state()
    {
        using var client = Client();
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, owner, "Entregas del recorrido", Ct);
        await AccountJourney.AuthorizeAsync(client, "business", Ct);
        var email = $"recorrido-entrega-{Guid.NewGuid():N}@example.test";
        var invitationId = await InviteAsync(client, email);
        var (memberId, outboxId) = await InvitationAsync(tenantId, invitationId);
        await OutboxRows.SetStatusAsync(factory.Services, outboxId, OutboxStatus.Sent, Ct);
        Assert.Equal("Sent", await DeliveryAsync(client, memberId));

        await OrderAsync(tenantId, new { kind = "invitation", email, outcome = "Uncertain" });
        Assert.Equal("Uncertain", await DeliveryAsync(client, memberId));
        Assert.Equal(("Email.Smtp.Uncertain", ""), await RowAsync(outboxId));

        await OrderAsync(tenantId, new { kind = "invitation", email, outcome = "Failed" });
        Assert.Equal("Failed", await DeliveryAsync(client, memberId));
        await OrderAsync(tenantId, new { kind = "invitation", email, outcome = "Failed", code = "Email.Smtp.Rejected" });
        Assert.Equal(("Email.Smtp.Rejected", ""), await RowAsync(outboxId));

        // De nuevo en la cola, con el próximo intento en un día: Usuarios la lee «Pendiente» y nadie la reclama todavía.
        await OrderAsync(tenantId, new { kind = "invitation", email, outcome = "Pending" });
        Assert.Equal("Pending", await DeliveryAsync(client, memberId));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var row = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutboxMessages.AsNoTracking()
                .SingleAsync(message => message.Id == outboxId, Ct);
            var nowUtc = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
            Assert.False(row.IsDue(nowUtc));
            Assert.Null(row.LastErrorCode);
        }
    }

    [Fact]
    public async Task The_order_rejects_what_is_not_a_test_destination_another_organization_or_a_bad_reason()
    {
        using var client = Client();
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, owner, "Entregas rechazadas", Ct);
        await AccountJourney.AuthorizeAsync(client, "business", Ct);
        using var otherClient = Client();
        var other = await AccountJourney.RegisterAsync(factory, otherClient, Ct);
        var otherTenant = await OrganizationJourney.StageAsync(factory, other, "Otra organización", Ct);
        var email = $"recorrido-rechazo-{Guid.NewGuid():N}@example.test";
        var (memberId, outboxId) = await InvitationAsync(tenantId, await InviteAsync(client, email));
        // Leer un código del pickup despacha la cola compartida: se fija el estado antes de las órdenes rechazadas.
        await OutboxRows.SetStatusAsync(factory.Services, outboxId, OutboxStatus.Sent, Ct);

        foreach (var command in new object[]
                 {
                     new { kind = "invitation", email = "persona@gmail.com", outcome = "Uncertain" },
                     new { kind = "invitation", email, outcome = "Delivered" },
                     new { kind = "invitation", email, outcome = "Uncertain", code = "Email.Smtp.Rejected" },
                     new { kind = "invitation", email, outcome = "Failed", code = "WhatsApp.Send.Undeliverable" },
                     new { kind = "invitation", email, phone = "+5491134567871", outcome = "Failed" },
                     new { kind = "invitation", email, outcome = "Failed", tenantId = otherTenant },
                     new { kind = "chatMessage", email, outcome = "Uncertain" },
                 })
            await Assert.ThrowsAsync<InvalidOperationException>(() => OrderAsync(tenantId, command));
        // La organización de la orden es la del recorrido: la invitación de otra no se encuentra.
        await Assert.ThrowsAsync<InvalidOperationException>(() => OrderAsync(otherTenant, new { kind = "invitation", email, outcome = "Uncertain" }));
        Assert.Equal("Sent", await DeliveryAsync(client, memberId));
    }

    private Task<object> OrderAsync(Guid tenantId, object command) =>
        RecorridoInvitationDelivery.ExecuteAsync(factory.Services, JsonSerializer.SerializeToElement(command), tenantId, Ct);

    private HttpClient Client() => factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private static async Task<Guid> InviteAsync(HttpClient client, string email)
    {
        using var issued = await AccountJourney.PostAsync(client, "/api/users/invitations",
            new { email, roleIds = Array.Empty<Guid>(), companies = Array.Empty<object>() }, Ct);
        Assert.True(issued.StatusCode == HttpStatusCode.Created, await issued.Content.ReadAsStringAsync(Ct));
        using var json = JsonDocument.Parse(await issued.Content.ReadAsStringAsync(Ct));
        return json.RootElement.GetProperty("invitationId").GetGuid();
    }

    private async Task<(Guid MemberId, Guid OutboxId)> InvitationAsync(Guid tenantId, Guid invitationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
        var invitation = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Invitations.AsNoTracking()
            .SingleAsync(row => row.Id == invitationId, Ct);
        return (invitation.MemberId, Assert.IsType<Guid>(invitation.OutboxMessageId));
    }

    private async Task<(string? Code, string Payload)> RowAsync(Guid outboxId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var row = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutboxMessages.AsNoTracking()
            .SingleAsync(message => message.Id == outboxId, Ct);
        return (row.LastErrorCode, row.EncryptedPayload);
    }

    private static async Task<string?> DeliveryAsync(HttpClient client, Guid memberId)
    {
        using var response = await client.GetAsync($"/api/users/{memberId:D}", Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return json.RootElement.GetProperty("invitationDelivery").GetString();
    }
}
