using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Platform;

/// <summary>
/// B6 (ADR 0066 §4): la invitación de un operador guarda, en su cuenta, el mensaje de la cola que la lleva, y Cuentas y la
/// ficha derivan de la cola su estado de entrega; reenviar sigue al mensaje nuevo. Sin invitación pendiente, null.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class PlatformOperatorInvitationDeliveryTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_operator_invitation_shows_each_queue_state_and_resending_follows_the_new_message()
    {
        using var manager = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(factory, manager, Ct);
        await PlatformOperators.GrantAsync(factory.Services, account.UserId, PlatformOperators.All, Ct);
        await AccountJourney.AuthorizeAsync(manager, "platform", Ct);
        var email = $"operador-entrega-{Guid.NewGuid():N}@example.test";
        using var invited = await PlatformJourney.PostAsync(manager, "/api/platform/operators/invitations",
            new { email, permissions = new[] { PlatformPermissions.Tenants.Read } }, Ct);
        Assert.True(invited.StatusCode == HttpStatusCode.Created, await invited.Content.ReadAsStringAsync(Ct));
        var id = JsonDocument.Parse(await invited.Content.ReadAsStringAsync(Ct)).RootElement.GetProperty("id").GetGuid();
        var outboxId = Assert.IsType<Guid>(await OutboxIdAsync(id));

        await AssertDeliveryAsync(manager, id, email, "Pending");
        foreach (var (status, expected) in new[]
                 {
                     (OutboxStatus.Sent, "Sent"), (OutboxStatus.Uncertain, "Uncertain"), (OutboxStatus.Failed, "Failed"),
                     (OutboxStatus.Cancelled, "Failed"),
                 })
        {
            await OutboxRows.SetStatusAsync(factory.Services, outboxId, status, Ct);
            await AssertDeliveryAsync(manager, id, email, expected);
        }

        // Vencida sigue pendiente de aceptar: conserva su estado de entrega.
        await using (var scope = factory.Services.CreateAsyncScope())
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users
                .Where(user => user.Id == id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.OperatorInvitationExpiresAtUtc,
                    factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime.AddDays(-1)), Ct));
        Assert.Equal("InvitationExpired", (await DetailAsync(manager, id)).GetProperty("state").GetString());
        await AssertDeliveryAsync(manager, id, email, "Failed");

        using var resent = await PlatformJourney.PostAsync(manager, $"/api/platform/operators/{id:D}/invitation/resend", new { }, Ct);
        Assert.True(resent.StatusCode == HttpStatusCode.OK, await resent.Content.ReadAsStringAsync(Ct));
        var renewed = Assert.IsType<Guid>(await OutboxIdAsync(id));
        Assert.NotEqual(outboxId, renewed);
        await AssertDeliveryAsync(manager, id, email, "Pending");
        await OutboxRows.DeleteAsync(factory, renewed, Ct);
        await AssertDeliveryAsync(manager, id, email, "Sent");

        // Quien no tiene una invitación de operador pendiente no tiene estado de entrega.
        Assert.Equal(JsonValueKind.Null, (await DetailAsync(manager, account.UserId)).GetProperty("invitationDelivery").ValueKind);
    }

    private async Task<Guid?> OutboxIdAsync(Guid accountId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.AsNoTracking()
            .Where(user => user.Id == accountId).Select(user => user.OperatorInvitationOutboxMessageId).SingleAsync(Ct);
    }

    private static async Task AssertDeliveryAsync(HttpClient client, Guid id, string email, string expected)
    {
        using var response = await client.GetAsync($"/api/platform/accounts/paged?search={Uri.EscapeDataString(email)}", Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var row = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetGuid() == id);
        Assert.Equal(expected, row.GetProperty("invitationDelivery").GetString());
        Assert.Equal(expected, (await DetailAsync(client, id)).GetProperty("invitationDelivery").GetString());
    }

    private static async Task<JsonElement> DetailAsync(HttpClient client, Guid id)
    {
        using var response = await client.GetAsync($"/api/platform/accounts/{id:D}", Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return json.RootElement.Clone();
    }
}
