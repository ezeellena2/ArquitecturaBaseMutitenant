using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Retention;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

/// <summary>
/// Apagar no borra datos (ADR 0069): Plataforma apaga WhatsApp de una organización con su <c>PUT</c>, sus rutas dan 404 y sus
/// chats, mensajes y contactos siguen en la base; la entrada que llega la guarda el webhook y el procesador no la atiende, la purga
/// de siempre vacía solo lo que venció, y al prenderlo otra vez se ve igual.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class WhatsAppModuleOffKeepsDataTests(ApiFactory factory)
{
    private const string Conversations = "/api/organization/whatsapp/customers/conversations";
    private const string Contacts = "/api/organization/whatsapp/contacts";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Turning_it_off_hides_its_routes_keeps_its_rows_lets_the_usual_purge_run_and_turning_it_on_shows_them_the_same()
    {
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        using var platform = WhatsAppAttentionJourney.Client(host);
        var operatorAccount = await AccountJourney.RegisterAsync(host, platform, Ct);
        await PlatformOperators.GrantAsync(host.Services, operatorAccount.UserId, PlatformOperators.All, Ct);
        await AccountJourney.AuthorizeAsync(platform, "platform", Ct);
        using var client = WhatsAppAttentionJourney.Client(host);
        var (_, tenant) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, client, Ct, enable: false);

        // Plataforma lo prende y la organización lo usa: su número y dos clientes que le escriben (dos chats y dos contactos).
        var version = await TurnAsync(platform, tenant, true, 0);
        var phoneNumberId = WhatsAppAttentionJourney.NewNumberId();
        using (var connected = await WhatsAppAttentionJourney.ConnectAsync(client, phoneNumberId, Ct))
            Assert.True(connected.IsSuccessStatusCode, $"{(int)connected.StatusCode}: {await connected.Content.ReadAsStringAsync(Ct)}");
        var kept = await ChatAsync(host, client, phoneNumberId, "kept");
        var expired = await ChatAsync(host, client, phoneNumberId, "expired");
        await AgeAsync(tenant, expired.Id, 4000);
        var before = await ViewAsync(client, kept.Id);
        Assert.NotNull(Item(before.Conversations, expired.Id));
        var rows = await CountAsync(host, tenant);
        Assert.Equal((2, 2), (rows.Chats, rows.Contacts));

        // Apagado: sus rutas dan 404 y sus filas siguen en la base.
        version = await TurnAsync(platform, tenant, false, version);
        foreach (var path in new[] { Conversations, $"{Conversations}/{kept.Id:D}", Contacts })
        {
            using var hidden = await client.GetAsync(path, Ct);
            Assert.True(hidden.StatusCode == HttpStatusCode.NotFound, $"{path}: {(int)hidden.StatusCode}");
        }
        Assert.Equal(rows, await CountAsync(host, tenant));

        // Los trabajos de fondo no lo atienden: el webhook guarda la entrada de un cliente nuevo (y su remitente), pero el
        // procesador no le abre chat ni le agrega mensajes.
        var wamid = await ReceiveAsync(host, phoneNumberId, WhatsAppAttentionJourney.NewPhone(), "Hola con el módulo apagado");
        rows = rows with { Contacts = rows.Contacts + 1 };
        Assert.Equal(rows, await CountAsync(host, tenant));
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var entry = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppMessage>().AsNoTracking()
                .SingleAsync(value => value.Wamid == wamid, Ct);
            Assert.Null(entry.ProcessedAtUtc);
            Assert.NotNull(entry.EncryptedText);
        }

        // La purga de siempre sigue con el módulo apagado: vacía el chat vencido y no toca el otro ni borra filas.
        var runner = host.Services.GetRequiredService<TenantJobRunner>();
        using var worker = new WhatsAppMessageRetentionWorker(host.Services.GetRequiredService<IServiceScopeFactory>(), runner,
            host.Services.GetRequiredService<TimeProvider>(), NullLogger<WhatsAppMessageRetentionWorker>.Instance);
        var cleared = 0;
        await runner.RunAsync([tenant], async (services, ct) => cleared += await worker.PurgeOrganizationAsync(services, ct), Ct);
        Assert.True(cleared > 0);
        Assert.Equal(new string?[] { null }, await TextsAsync(host, tenant, expired.Id));
        Assert.Equal(new string?[] { kept.Mark }, await TextsAsync(host, tenant, kept.Id));
        Assert.Equal(rows, await CountAsync(host, tenant));

        // Prendido otra vez, se ve igual.
        await TurnAsync(platform, tenant, true, version);
        var after = await ViewAsync(client, kept.Id);
        Assert.Equal(before.Detail, after.Detail);
        Assert.Equal(Item(before.Conversations, kept.Id), Item(after.Conversations, kept.Id));
        Assert.NotNull(Item(after.Conversations, expired.Id));
        var contacts = before.Contacts.GetProperty("contacts").GetProperty("items").EnumerateArray()
            .Select(row => row.GetProperty("id").GetGuid()).ToArray();
        Assert.Equal(2, contacts.Length);
        Assert.All(contacts, id => Assert.Equal(Item(before.Contacts, id), Item(after.Contacts, id)));
    }

    /// <summary>Plataforma prende o apaga el módulo con la pestaña «Módulos», con la versión que leyó.</summary>
    private static async Task<uint> TurnAsync(HttpClient platform, Guid tenant, bool enabled, uint version)
    {
        using var saved = await platform.PutAsJsonAsync($"/api/platform/tenants/{tenant:D}/features/{WhatsAppFeature.Key}",
            new { enabled, source = "Manual", version, reason = enabled ? "Prender WhatsApp" : "Apagar WhatsApp" }, Ct);
        return await SavedVersion.ReadAsync(saved, Ct);
    }

    private sealed record View(JsonElement Conversations, string Detail, JsonElement Contacts);

    private static async Task<View> ViewAsync(HttpClient client, Guid chatId)
    {
        var detail = await ReadAsync(client, $"{Conversations}/{chatId:D}");
        return new(JsonDocument.Parse(await ReadAsync(client, Conversations)).RootElement.Clone(), detail,
            JsonDocument.Parse(await ReadAsync(client, Contacts)).RootElement.Clone());
    }

    private static async Task<string> ReadAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path, Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path}: {(int)response.StatusCode} {body}");
        return body;
    }

    /// <summary>El elemento de una lista con ese id, en texto, o null si no está.</summary>
    private static string? Item(JsonElement root, Guid id) => root.ValueKind switch
    {
        JsonValueKind.Object when root.TryGetProperty("id", out var value) && value.ValueKind == JsonValueKind.String
            && value.GetGuid() == id => root.GetRawText(),
        JsonValueKind.Object => root.EnumerateObject().Select(property => Item(property.Value, id)).FirstOrDefault(found => found is not null),
        JsonValueKind.Array => root.EnumerateArray().Select(element => Item(element, id)).FirstOrDefault(found => found is not null),
        _ => null,
    };

    /// <summary>Un cliente nuevo le escribe al número de la organización: un chat con un solo mensaje entrante.</summary>
    private static async Task<(Guid Id, string Mark)> ChatAsync(WebApplicationFactory<Program> host, HttpClient client, string phoneNumberId,
        string label)
    {
        var customer = WhatsAppAttentionJourney.NewPhone();
        var mark = $"{label}-{Guid.NewGuid():N}";
        using var anonymous = WhatsAppAttentionJourney.Client(host);
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, customer, mark, Ct);
        var list = JsonDocument.Parse(await ReadAsync(client, Conversations)).RootElement;
        var id = list.GetProperty("conversations").GetProperty("items").EnumerateArray()
            .Single(row => row.GetProperty("displayNumber").GetString() == customer).GetProperty("id").GetGuid();
        return (id, mark);
    }

    /// <summary>La entrada de un cliente por el webhook, con el procesador que la atiende; devuelve su identificador de Meta.</summary>
    private static async Task<string> ReceiveAsync(WebApplicationFactory<Program> host, string phoneNumberId, string from, string text)
    {
        var wamid = "wamid.off." + Guid.NewGuid().ToString("N");
        var body = JsonSerializer.Serialize(new
        {
            @object = "whatsapp_business_account",
            entry = new[] { new { id = "waba-" + phoneNumberId, changes = new[] { new { field = "messages", value = new
            {
                messaging_product = "whatsapp",
                metadata = new { display_phone_number = "5491100000000", phone_number_id = phoneNumberId },
                messages = new[] { new
                {
                    from = from.TrimStart('+'), id = wamid, type = "text", text = new { body = text },
                    timestamp = TimeProvider.System.GetUtcNow().ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
                } },
            } } } } },
        });
        using var anonymous = WhatsAppAttentionJourney.Client(host);
        using (var received = await WhatsAppAttentionJourney.WebhookAsync(anonymous, body, Ct))
            Assert.Equal(HttpStatusCode.OK, received.StatusCode);
        await host.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>()
            .OfType<ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Inbound.WhatsAppInboundProcessor>().Single().ProcessPendingAsync(Ct);
        return wamid;
    }

    /// <summary>El chat y sus mensajes pasan a tener la edad pedida (mt_owner respeta la RLS: con la organización en el alcance).</summary>
    private async Task AgeAsync(Guid tenantId, Guid chatId, int days)
    {
        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(Ct);
        await using var transaction = await connection.BeginTransactionAsync(Ct);
        await using (var scope = new NpgsqlCommand("SELECT set_config('app.tenant_id', @tenant, true)", connection, transaction))
        {
            scope.Parameters.AddWithValue("tenant", tenantId.ToString("D"));
            await scope.ExecuteScalarAsync(Ct);
        }
        foreach (var sql in new[]
        {
            """UPDATE tenant."WhatsAppChatMessages" SET "CreatedAtUtc" = "CreatedAtUtc" - @days * interval '1 day' WHERE "ChatId" = @chat""",
            """UPDATE tenant."WhatsAppChats" SET "LastMessageAtUtc" = "LastMessageAtUtc" - @days * interval '1 day' WHERE "Id" = @chat""",
        })
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("days", days);
            command.Parameters.AddWithValue("chat", chatId);
            Assert.True(await command.ExecuteNonQueryAsync(Ct) > 0);
        }
        await transaction.CommitAsync(Ct);
    }

    private sealed record Rows(int Chats, int Messages, int Contacts);

    private static async Task<Rows> CountAsync(WebApplicationFactory<Program> host, Guid tenant)
    {
        await using var scope = host.Services.CreateAsyncScope();
        using var enter = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenant);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return new(await db.Set<WhatsAppChat>().CountAsync(Ct), await db.Set<WhatsAppChatMessage>().CountAsync(Ct),
            await db.Set<WhatsAppContact>().CountAsync(Ct));
    }

    private static async Task<string?[]> TextsAsync(WebApplicationFactory<Program> host, Guid tenant, Guid chatId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        using var enter = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenant);
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppChatMessage>().AsNoTracking()
            .Where(message => message.ChatId == chatId && message.Direction == WhatsAppMessageDirection.Inbound)
            .Select(message => message.Text).ToArrayAsync(Ct);
    }
}
