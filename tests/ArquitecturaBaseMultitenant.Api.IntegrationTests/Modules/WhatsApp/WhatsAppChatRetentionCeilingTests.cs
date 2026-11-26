using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
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
/// El plazo de los chats de una organización pasa por el resolvedor de los topes (ADR 0069 §4, B5): guardar más que el tope es el
/// 400 de siempre en su campo; si Plataforma baja el máximo, el GET y la purga de cada organización usan el menor sin tocar lo
/// guardado, cada una con lo suyo; los chats de la plataforma siguen con su propio plazo. Cada prueba tiene su base: el máximo de
/// la plataforma es global.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class WhatsAppChatRetentionCeilingTests
{
    private const string OrganizationSettings = "/api/organization/whatsapp/settings";
    private const string PlatformSettings = "/api/platform/whatsapp/settings";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Saving_more_than_the_ceiling_is_a_400_on_its_field_and_a_lower_maximum_trims_the_get_without_touching_what_was_saved()
    {
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        await using var host = WhatsAppAttentionJourney.Host(isolated, new FakeGraph());
        using var platform = await OperatorAsync(host);
        using var client = WhatsAppAttentionJourney.Client(host);
        var (_, tenant) = await WhatsAppAttentionJourney.OrganizationAsync(isolated, host, client, Ct);
        await PlatformAsync(platform, 90, 30);

        using (var over = await ChooseAsync(client, 31))
        {
            Assert.Equal(HttpStatusCode.BadRequest, over.StatusCode);
            using var problem = JsonDocument.Parse(await over.Content.ReadAsStringAsync(Ct));
            Assert.Equal(WhatsAppErrors.ChatRetentionOutOfRangeCode, problem.RootElement.GetProperty("code").GetString());
            Assert.Contains("30", problem.RootElement.GetProperty("errors").GetProperty("chatRetentionDays")[0].GetString(), StringComparison.Ordinal);
        }
        using (var top = await ChooseAsync(client, 30))
            Assert.True(top.StatusCode == HttpStatusCode.OK, $"{(int)top.StatusCode}: {await top.Content.ReadAsStringAsync(Ct)}");

        await PlatformAsync(platform, 90, 20);
        var current = await JsonAsync(client.GetAsync(OrganizationSettings, Ct));
        Assert.Equal((20, 20), (current.GetProperty("chatRetentionDays").GetInt32(), current.GetProperty("maxOrganizationChatRetentionDays").GetInt32()));
        Assert.Equal(30, await SavedAsync(host.Services, tenant));
    }

    [Fact]
    public async Task A_lower_maximum_purges_with_the_lesser_term_in_its_organization_only_and_platform_chats_keep_their_own_term()
    {
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        await using var host = WhatsAppAttentionJourney.Host(isolated, new FakeGraph());
        using var platform = await OperatorAsync(host);
        var a = await OrganizationAsync(isolated, host, 60, "A");
        var b = await OrganizationAsync(isolated, host, 60, "B");
        // A: un chat de hace 40 días y otro de hace 25. B: uno de hace 40. Los dos eligieron 60.
        var aOld = await ChatAsync(host, a, "A-old");
        var aYoung = await ChatAsync(host, a, "A-young");
        var bOld = await ChatAsync(host, b, "B-old");
        await AgeAsync(isolated, a.TenantId, aOld.Id, 40);
        await AgeAsync(isolated, a.TenantId, aYoung.Id, 25);
        await AgeAsync(isolated, b.TenantId, bOld.Id, 40);
        // Plataforma: una consulta de hace 60 días y otra de hace 100, con su plazo de 90.
        var (platformKept, platformExpired) = await PlatformChatsAsync(host.Services, a.PhoneNumberId);

        var runner = host.Services.GetRequiredService<TenantJobRunner>();
        using var worker = new WhatsAppMessageRetentionWorker(host.Services.GetRequiredService<IServiceScopeFactory>(), runner,
            host.Services.GetRequiredService<TimeProvider>(), NullLogger<WhatsAppMessageRetentionWorker>.Instance);

        // Con el máximo de entrada (3650), nadie vacía un mensaje antes del plazo que eligió.
        await runner.RunAsync([a.TenantId, b.TenantId], async (services, ct) => Assert.Equal(0, await worker.PurgeOrganizationAsync(services, ct)), Ct);

        // Plataforma baja el máximo a 30 sin tocar el plazo de sus propios chats: en A vale 30, el menor, solo en el alcance de A.
        await PlatformAsync(platform, 90, 30);
        var cleared = 0;
        await runner.RunAsync([a.TenantId], async (services, ct) => cleared += await worker.PurgeOrganizationAsync(services, ct), Ct);
        Assert.True(cleared > 0);
        Assert.Equal(new string?[] { null }, await TextsAsync(host.Services, a.TenantId, aOld.Id));
        Assert.Equal(new string?[] { aYoung.Mark }, await TextsAsync(host.Services, a.TenantId, aYoung.Id));
        Assert.Equal(new string?[] { bOld.Mark }, await TextsAsync(host.Services, b.TenantId, bOld.Id));
        Assert.Equal(60, await SavedAsync(host.Services, a.TenantId));

        // La pasada de B vacía lo suyo con su menor; repetir no cambia nada.
        await runner.RunAsync([b.TenantId], async (services, ct) => Assert.True(await worker.PurgeOrganizationAsync(services, ct) > 0), Ct);
        Assert.Equal(new string?[] { null }, await TextsAsync(host.Services, b.TenantId, bOld.Id));
        await runner.RunAsync([a.TenantId, b.TenantId], async (services, ct) => Assert.Equal(0, await worker.PurgeOrganizationAsync(services, ct)), Ct);
        Assert.Equal(new string?[] { aYoung.Mark }, await TextsAsync(host.Services, a.TenantId, aYoung.Id));

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var retention = scope.ServiceProvider.GetRequiredService<IWhatsAppMessageRetentionService>();
            while ((await retention.ClearExpiredPlatformChatsAsync(Ct)).Value > 0) { }
        }
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(platformKept.Text, await db.Set<PlatformWhatsAppChatMessage>().Where(message => message.ChatId == platformKept.ChatId)
                .Select(message => message.Text).SingleAsync(Ct));
            Assert.Null(await db.Set<PlatformWhatsAppChatMessage>().Where(message => message.ChatId == platformExpired.ChatId)
                .Select(message => message.Text).SingleAsync(Ct));
        }
    }

    private static async Task<HttpClient> OperatorAsync(WebApplicationFactory<Program> host)
    {
        var client = WhatsAppAttentionJourney.Client(host);
        var account = await AccountJourney.RegisterAsync(host, client, Ct);
        await PlatformOperators.GrantAsync(host.Services, account.UserId, PlatformOperators.All, Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        return client;
    }

    /// <summary>Plataforma guarda su configuración por su pantalla, con la versión que leyó.</summary>
    private static async Task PlatformAsync(HttpClient platform, int chatRetentionDays, int maxOrganizationChatRetentionDays)
    {
        var version = (await JsonAsync(platform.GetAsync(PlatformSettings, Ct))).GetProperty("version").GetUInt32();
        using var saved = await platform.PutAsJsonAsync(PlatformSettings, new
        {
            allowedCountries = new[] { "AR" }, chatRetentionDays, maxOrganizationChatRetentionDays, version,
        }, Ct);
        Assert.True(saved.StatusCode == HttpStatusCode.OK, $"{(int)saved.StatusCode}: {await saved.Content.ReadAsStringAsync(Ct)}");
    }

    private static async Task<HttpResponseMessage> ChooseAsync(HttpClient client, int chatRetentionDays)
    {
        var version = (await JsonAsync(client.GetAsync(OrganizationSettings, Ct))).GetProperty("version").GetUInt32();
        return await client.PutAsJsonAsync(OrganizationSettings, new { chatRetentionDays, version }, Ct);
    }

    /// <summary>Una organización con WhatsApp prendido, su número conectado y el plazo que eligió su Dueño.</summary>
    private static async Task<World> OrganizationAsync(ApiFactory factory, WebApplicationFactory<Program> host, int chosen, string label)
    {
        var client = WhatsAppAttentionJourney.Client(host);
        var (_, tenant) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, client, Ct);
        var phoneNumberId = WhatsAppAttentionJourney.NewNumberId();
        (await WhatsAppAttentionJourney.ConnectAsync(client, phoneNumberId, Ct)).Dispose();
        using (var saved = await ChooseAsync(client, chosen))
            Assert.True(saved.StatusCode == HttpStatusCode.OK, $"{label} {(int)saved.StatusCode}: {await saved.Content.ReadAsStringAsync(Ct)}");
        return new World(client, tenant, phoneNumberId);
    }

    /// <summary>Un cliente nuevo le escribe al número de la organización: un chat con un solo mensaje entrante.</summary>
    private static async Task<(Guid Id, string Mark)> ChatAsync(WebApplicationFactory<Program> host, World world, string label)
    {
        var customer = WhatsAppAttentionJourney.NewPhone();
        var mark = $"{label}-{Guid.NewGuid():N}";
        using var anonymous = WhatsAppAttentionJourney.Client(host);
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, world.PhoneNumberId, customer, mark, Ct);
        var list = await JsonAsync(world.Client.GetAsync("/api/organization/whatsapp/customers/conversations", Ct));
        var id = list.GetProperty("conversations").GetProperty("items").EnumerateArray()
            .Single(row => row.GetProperty("displayNumber").GetString() == customer).GetProperty("id").GetGuid();
        return (id, mark);
    }

    /// <summary>El chat y sus mensajes pasan a tener la edad pedida (mt_owner respeta la RLS: con la organización en el alcance).</summary>
    private static async Task AgeAsync(ApiFactory factory, Guid tenantId, Guid chatId, int days)
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

    private static async Task<((Guid ChatId, string Text) Kept, (Guid ChatId, string Text) Expired)> PlatformChatsAsync(
        IServiceProvider root, string phoneNumberId)
    {
        await using var scope = root.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var channel = await db.Set<WhatsAppChannel>().AsNoTracking().SingleAsync(value => value.PhoneNumberId == phoneNumberId, Ct);
        (Guid, string) Add(int age)
        {
            var text = $"platform-{age}-{Guid.NewGuid():N}";
            var chat = PlatformWhatsAppChat.Create(channel.Id, PhoneNumber.Create(WhatsAppAttentionJourney.NewPhone()).Value, now.AddDays(-age));
            chat.RecordInbound(now.AddDays(-age), text);
            db.AddRange(chat, PlatformWhatsAppChatMessage.Inbound(chat.Id, "wamid.b5." + Guid.NewGuid().ToString("N"), text, null, now.AddDays(-age)));
            return (chat.Id, text);
        }
        (Guid, string) kept = default, expired = default;
        var saved = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            (kept, expired) = (Add(60), Add(100));
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(saved.IsSuccess);
        return (kept, expired);
    }

    private static async Task<string?[]> TextsAsync(IServiceProvider root, Guid tenantId, Guid chatId)
    {
        await using var scope = root.CreateAsyncScope();
        using var enter = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppChatMessage>().AsNoTracking()
            .Where(message => message.ChatId == chatId && message.Direction == WhatsAppMessageDirection.Inbound)
            .Select(message => message.Text).ToArrayAsync(Ct);
    }

    private static async Task<int?> SavedAsync(IServiceProvider root, Guid tenantId)
    {
        await using var scope = root.CreateAsyncScope();
        using var enter = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppOrganizationSettings>().AsNoTracking()
            .Select(settings => (int?)settings.ChatRetentionDays).SingleOrDefaultAsync(Ct);
    }

    private static async Task<JsonElement> JsonAsync(Task<HttpResponseMessage> request)
    {
        using var response = await request;
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return await WhatsAppAttentionJourney.JsonAsync(response, Ct);
    }

    private sealed record World(HttpClient Client, Guid TenantId, string PhoneNumberId);
}
