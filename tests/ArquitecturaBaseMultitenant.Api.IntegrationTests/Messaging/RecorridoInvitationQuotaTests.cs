using System.Globalization;
using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Recorrido;
using ArquitecturaBaseMultitenant.Domain.Messaging;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Messaging;

/// <summary>
/// La orden privada <c>invitation-quota</c> del recorrido (B-3 de la puerta de la base 2): con los topes altos del entorno,
/// deja a quien invita a una invitación de su tope de 24 horas (ADR 0063 y 256). La que sigue entra y la próxima responde el
/// 429 con una espera de horas, por la ruta real; al soltarla, se vuelve a invitar. No toca la configuración de la app.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class RecorridoInvitationQuotaTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Filling_the_inviter_quota_leaves_one_invitation_then_the_wait_is_in_hours_until_released()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        await OrganizationJourney.StageAsync(factory, owner, "Tope del recorrido", Ct);
        await AccountJourney.AuthorizeAsync(client, "business", Ct);
        var quota = new RecorridoInvitationQuota(factory.Services);
        var inviter = owner.Email.Value;

        Assert.Equal(1, Json(await quota.ExecuteAsync(Command(new { kind = "fill", email = inviter, remaining = 1 }), Ct)).GetProperty("remaining").GetInt32());
        await Assert.ThrowsAsync<InvalidOperationException>(() => quota.ExecuteAsync(Command(new { kind = "fill", email = inviter, remaining = 1 }), Ct));
        using (var last = await InviteAsync(client))
            Assert.True(last.StatusCode == HttpStatusCode.Created, await last.Content.ReadAsStringAsync(Ct));
        using (var full = await InviteAsync(client))
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, full.StatusCode);
            using var problem = JsonDocument.Parse(await full.Content.ReadAsStringAsync(Ct));
            Assert.Equal(EmailQuotaErrors.DailyLimitCode, problem.RootElement.GetProperty("code").GetString());
            // La hora anterior a la actual sale de la ventana en unas 23 horas: la espera se dice en horas.
            var wait = problem.RootElement.GetProperty(EmailQuotaErrors.RetryAfterKey).GetInt32();
            Assert.InRange(wait, 22 * 3600, 24 * 3600);
            Assert.Equal(wait.ToString(CultureInfo.InvariantCulture), Assert.Single(full.Headers.GetValues("Retry-After")));
        }

        Assert.True(Json(await quota.ExecuteAsync(Command(new { kind = "release", email = inviter }), Ct)).GetProperty("released").GetBoolean());
        using (var again = await InviteAsync(client))
            Assert.True(again.StatusCode == HttpStatusCode.Created, await again.Content.ReadAsStringAsync(Ct));
        Assert.False(Json(await quota.ExecuteAsync(Command(new { kind = "release", email = inviter }), Ct)).GetProperty("released").GetBoolean());
    }

    [Fact]
    public async Task The_order_rejects_another_mailbox_or_more_than_ten_places()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = (await AccountJourney.RegisterAsync(factory, client, Ct)).Email.Value;
        var quota = new RecorridoInvitationQuota(factory.Services);
        foreach (var command in new object[]
                 {
                     new { kind = "fill", email = "persona@gmail.com", remaining = 1 },
                     new { kind = "fill", email = owner, remaining = 11 },
                     new { kind = "fill", email = owner },
                     new { kind = "empty", email = owner },
                     new { kind = "release", email = owner, remaining = 1 },
                     new { kind = "fill", email = owner, remaining = 1, tenantId = Guid.NewGuid() },
                 })
            await Assert.ThrowsAsync<InvalidOperationException>(() => quota.ExecuteAsync(Command(command), Ct));
        // Nada quedó lleno: la próxima orden válida llena y suelta.
        await quota.ExecuteAsync(Command(new { kind = "fill", email = owner, remaining = 0 }), Ct);
        Assert.True(Json(await quota.ExecuteAsync(Command(new { kind = "release", email = owner }), Ct)).GetProperty("released").GetBoolean());
    }

    private static JsonElement Command(object value) => JsonSerializer.SerializeToElement(value);

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);

    private static Task<HttpResponseMessage> InviteAsync(HttpClient client) => AccountJourney.PostAsync(client, "/api/users/invitations",
        new { email = $"recorrido-tope-{Guid.NewGuid():N}@example.test", roleIds = Array.Empty<Guid>(), companies = Array.Empty<object>() }, Ct);
}
