using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Retention;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

/// <summary>
/// ADR 0065 (B8): el contenido del chat se guarda cifrado con el propósito de su esquema y la organización de la base que lee o
/// guarda, en la petición, en <c>ITenantScope.Enter</c>, en <c>TenantJobRunner</c> y con dos bases a la vez. El SQL crudo no
/// muestra el texto; un cifrado copiado a otra organización (o de Plataforma a una organización) se lee vacío; la bandeja busca
/// por el nombre y el número, nunca por lo que se escribió.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class WhatsAppChatContentIsolationTests(ApiFactory factory)
{
    private const string Customers = "/api/organization/whatsapp/customers";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Raw_sql_shows_no_text_and_a_cipher_copied_to_another_organization_reads_empty()
    {
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        var a = await OrganizationAsync(host, "A");
        var b = await OrganizationAsync(host, "B");

        // La petición: el texto sale como se escribió, en la bandeja y en la conversación.
        // La vista previa es la del último mensaje: la respuesta automática del bot, que también va cifrada.
        var preview = Chat(await JsonAsync(a.Client.GetAsync($"{Customers}/conversations", Ct)), a.Customer)
            .GetProperty("lastMessagePreview").GetString();
        Assert.False(string.IsNullOrEmpty(preview));
        Assert.Equal(a.Marks, Texts(await JsonAsync(a.Client.GetAsync($"{Customers}/conversations/{a.ChatId}", Ct))));

        // El SQL crudo, con la organización en el alcance, solo ve bytes que no contienen el texto.
        var rawA = await ContentAsync(a.TenantId, a.ChatId);
        Assert.All(rawA.Texts, cipher => Assert.True(Hides(cipher, [.. a.Marks, preview!])));
        Assert.True(Hides(rawA.Preview, [.. a.Marks, preview!]));

        // Un cifrado de A copiado a las filas de B se lee vacío en B, sin romper su conversación; A sigue leyendo el suyo.
        await RawAsync(b.TenantId, """
            UPDATE tenant."WhatsAppChatMessages" SET "Text" = @cipher WHERE "ChatId" = @chat;
            UPDATE tenant."WhatsAppChats" SET "LastMessagePreview" = @cipher WHERE "Id" = @chat;
            """, ("cipher", rawA.Preview), ("chat", b.ChatId));
        var listB = await JsonAsync(b.Client.GetAsync($"{Customers}/conversations", Ct));
        Assert.Equal(JsonValueKind.Null, Chat(listB, b.Customer).GetProperty("lastMessagePreview").ValueKind);
        Assert.All(Texts(await JsonAsync(b.Client.GetAsync($"{Customers}/conversations/{b.ChatId}", Ct))), Assert.Null);
        Assert.Equal(a.Marks, Texts(await JsonAsync(a.Client.GetAsync($"{Customers}/conversations/{a.ChatId}", Ct))));
    }

    [Fact]
    public async Task The_inbox_searches_by_name_and_number_and_never_by_content()
    {
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        var a = await OrganizationAsync(host, "A");
        var other = WhatsAppAttentionJourney.NewPhone();
        using var anonymous = WhatsAppAttentionJourney.Client(host);
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, a.PhoneNumberId, other, "Otro chat", Ct);
        var name = "Caro " + Guid.NewGuid().ToString("N")[..6];
        await InScopeAsync(a.TenantId, async (services, db) =>
            (await db.Set<WhatsAppCustomer>().SingleAsync(customer => customer.WaId == a.Customer, Ct)).Rename(name));

        Assert.Equal([a.Customer], await SearchAsync(a.Client, name));
        Assert.Equal([other], await SearchAsync(a.Client, other[^8..]));
        // Lo que se escribió no se busca: está cifrado y la bandeja no lo mira.
        Assert.Empty(await SearchAsync(a.Client, a.Marks[0]));
    }

    [Fact]
    public async Task Each_database_converts_with_its_own_organization_in_enter_the_job_runner_and_side_by_side()
    {
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        var a = await OrganizationAsync(host, "A");
        var b = await OrganizationAsync(host, "B");

        // ITenantScope.Enter, una base por vez.
        Assert.Equal(a.Marks, await TextsAsync(host.Services, a.TenantId));
        Assert.Equal(b.Marks, await TextsAsync(host.Services, b.TenantId));

        // Dos bases recorridas a la vez en el mismo flujo, paso por paso: cada paso lee con su organización.
        await using (var scopeA = host.Services.CreateAsyncScope())
        await using (var scopeB = host.Services.CreateAsyncScope())
        {
            using var enterA = scopeA.ServiceProvider.GetRequiredService<ITenantScope>().Enter(a.TenantId);
            using var enterB = scopeB.ServiceProvider.GetRequiredService<ITenantScope>().Enter(b.TenantId);
            var (seenA, seenB) = (new List<string?>(), new List<string?>());
            await using (var readA = Ordered(scopeA.ServiceProvider).AsAsyncEnumerable().GetAsyncEnumerator(Ct))
            await using (var readB = Ordered(scopeB.ServiceProvider).AsAsyncEnumerable().GetAsyncEnumerator(Ct))
                while (await readA.MoveNextAsync() & await readB.MoveNextAsync())
                {
                    seenA.Add(readA.Current);
                    seenB.Add(readB.Current);
                }
            Assert.Equal(a.Marks, seenA);
            Assert.Equal(b.Marks, seenB);
            // Lo que arranca enseguida se lleva su base: las dos consultas van a la vez.
            var (firstA, firstB) = (Ordered(scopeA.ServiceProvider).FirstAsync(Ct), Ordered(scopeB.ServiceProvider).FirstAsync(Ct));
            Assert.Equal((a.Marks[0], b.Marks[0]), (await firstA, await firstB));
        }

        // Muchas bases en paralelo, que además guardan: cada una cifra y lee con la suya.
        var notes = await Task.WhenAll(Enumerable.Range(0, 8).Select(index => Task.Run(async () =>
        {
            var world = index % 2 == 0 ? a : b;
            var note = $"nota-{world.Label}-{Guid.NewGuid():N}";
            await InScopeAsync(world.TenantId, (services, db) =>
            {
                db.Add(WhatsAppChatMessage.Note(world.ChatId, world.OwnerUserId, null, note,
                    services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
                return Task.CompletedTask;
            });
            Assert.Contains(note, await TextsAsync(host.Services, world.TenantId, inboundOnly: false));
            return (world.TenantId, Note: note);
        }, Ct)));
        foreach (var (tenantId, note) in notes)
            Assert.All((await ContentAsync(tenantId, tenantId == a.TenantId ? a.ChatId : b.ChatId)).Texts,
                cipher => Assert.True(Hides(cipher, [note])));

        // TenantJobRunner: cada organización en su alcance ve solo lo suyo, legible.
        var byJob = new Dictionary<Guid, string?[]>();
        await host.Services.GetRequiredService<TenantJobRunner>().RunAsync([a.TenantId, b.TenantId], async (services, ct) =>
            byJob[services.GetRequiredService<ITenantContext>().RequiredTenantId] = await services.GetRequiredService<ApplicationDbContext>()
                .Set<WhatsAppChatMessage>().AsNoTracking().Where(message => message.Direction == WhatsAppMessageDirection.Inbound)
                .OrderBy(message => message.CreatedAtUtc).ThenBy(message => message.Id).Select(message => message.Text).ToArrayAsync(ct), Ct);
        Assert.Equal(a.Marks, byJob[a.TenantId]);
        Assert.Equal(b.Marks, byJob[b.TenantId]);
    }

    [Fact]
    public async Task Platform_and_the_linked_copy_have_their_own_purpose()
    {
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        var a = await OrganizationAsync(host, "A");
        var (platformText, linkedText) = ("plataforma-" + Guid.NewGuid().ToString("N"), "vinculado-" + Guid.NewGuid().ToString("N"));

        // Plataforma: sin organización en el alcance.
        PlatformWhatsAppChat? platformChat = null;
        await InScopeAsync(null, async (services, db) =>
        {
            var now = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
            var channel = await db.Set<WhatsAppChannel>().AsNoTracking().SingleAsync(value => value.PhoneNumberId == a.PhoneNumberId, Ct);
            platformChat = PlatformWhatsAppChat.Create(channel.Id, PhoneNumber.Create(WhatsAppAttentionJourney.NewPhone()).Value, now);
            platformChat.RecordInbound(now, platformText);
            db.AddRange(platformChat, PlatformWhatsAppChatMessage.Inbound(platformChat.Id, "wamid.b8." + Guid.NewGuid().ToString("N"),
                platformText, null, now));
        });
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(platformText, await db.Set<PlatformWhatsAppChatMessage>().Where(message => message.ChatId == platformChat!.Id)
                .Select(message => message.Text).SingleAsync(Ct));
            Assert.Equal(platformText, (await db.Set<PlatformWhatsAppChat>().SingleAsync(chat => chat.Id == platformChat!.Id, Ct)).LastMessagePreview);
        }
        var platformCipher = await ScalarAsync(null, """SELECT "Text" FROM platform."PlatformWhatsAppChatMessages" WHERE "ChatId" = @chat""",
            ("chat", platformChat!.Id));
        Assert.True(Hides(platformCipher, [platformText]));

        // La copia vinculada: la guarda la organización y la lee la persona desde su espacio personal.
        await InScopeAsync(a.TenantId, (services, db) =>
        {
            var now = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
            var link = WhatsAppConversationLink.Create(a.PersonalTenantId, a.ChatId, a.OwnerUserId, "Persona", "Organización A", a.Customer, now);
            link.RecordMessage(now, linkedText);
            db.AddRange(link, WhatsAppLinkedMessage.Copy(link, WhatsAppMessageDirection.Inbound, linkedText, null, now));
            return Task.CompletedTask;
        }, TenantKind.Business);
        await using (var scope = host.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(a.PersonalTenantId, TenantKind.Personal);
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(linkedText, await db.Set<WhatsAppLinkedMessage>().Select(message => message.Text).SingleAsync(Ct));
            Assert.Equal(linkedText, await db.Set<WhatsAppConversationLink>().Select(link => link.LastMessagePreview).SingleAsync(Ct));
        }
        Assert.True(Hides(await ScalarAsync(a.PersonalTenantId, """SELECT "Text" FROM engagement."WhatsAppLinkedMessages" """), [linkedText]));

        // Un cifrado de Plataforma copiado a una organización, o de la copia vinculada, se lee vacío: el propósito es otro.
        var linkedCipher = await ScalarAsync(a.PersonalTenantId, """SELECT "Text" FROM engagement."WhatsAppLinkedMessages" """);
        var firstMessage = (await ContentAsync(a.TenantId, a.ChatId)).Ids[0];
        await RawAsync(a.TenantId, """UPDATE tenant."WhatsAppChatMessages" SET "Text" = @cipher WHERE "Id" = @id""",
            ("cipher", platformCipher), ("id", firstMessage));
        await RawAsync(a.TenantId, """UPDATE tenant."WhatsAppChats" SET "LastMessagePreview" = @cipher WHERE "Id" = @id""",
            ("cipher", linkedCipher), ("id", a.ChatId));
        await using (var scope = host.Services.CreateAsyncScope())
        {
            using var enter = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(a.TenantId);
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Null(await db.Set<WhatsAppChatMessage>().Where(message => message.Id == firstMessage).Select(message => message.Text).SingleAsync(Ct));
            Assert.Null((await db.Set<WhatsAppChat>().SingleAsync(chat => chat.Id == a.ChatId, Ct)).LastMessagePreview);
        }
    }

    [Fact]
    public async Task Content_of_an_organization_without_its_organization_or_outside_its_database_fails()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Throws<InvalidOperationException>(() => WhatsAppChatContentConverter.Organization.ConvertToProvider("hola"));
        using (PersistenceOperation.Enter(db))
        {
            Assert.Throws<InvalidOperationException>(() => WhatsAppChatContentConverter.Organization.ConvertToProvider("hola"));
            // Plataforma y la copia vinculada no dependen de una organización.
            var platform = (byte[])WhatsAppChatContentConverter.Platform.ConvertToProvider("hola")!;
            Assert.Equal("hola", WhatsAppChatContentConverter.Platform.ConvertFromProvider(platform));
            Assert.Null(WhatsAppChatContentConverter.Linked.ConvertFromProvider(platform));
            Assert.Null(WhatsAppChatContentConverter.Platform.ConvertFromProvider(Encoding.UTF8.GetBytes("hola")));
        }
    }

    [Fact]
    public async Task Chat_retention_clears_each_organization_with_its_own_term_and_the_platform_with_its_own()
    {
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        var a = await OrganizationAsync(host, "RA");
        var b = await OrganizationAsync(host, "RB");
        var linkedText = "vinculado-" + Guid.NewGuid().ToString("N");
        // A guarda 30 días; B no eligió y vale el de defecto (90). Todo lo de las dos tiene 60 días.
        await InScopeAsync(a.TenantId, (_, db) =>
        {
            var settings = WhatsAppOrganizationSettings.Create();
            Assert.True(settings.Update(30, 3650));
            db.Add(settings);
            return Task.CompletedTask;
        });
        await InScopeAsync(a.TenantId, (services, db) =>
        {
            var now = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
            var link = WhatsAppConversationLink.Create(a.PersonalTenantId, a.ChatId, a.OwnerUserId, "Persona", "Organización A", a.Customer, now);
            link.RecordMessage(now, linkedText);
            db.AddRange(link, WhatsAppLinkedMessage.Copy(link, WhatsAppMessageDirection.Inbound, linkedText, null, now));
            return Task.CompletedTask;
        }, TenantKind.Business);
        foreach (var world in new[] { a, b })
        {
            await RawAsync(world.TenantId, """UPDATE tenant."WhatsAppChatMessages" SET "CreatedAtUtc" = "CreatedAtUtc" - interval '60 days' WHERE "ChatId" = @chat""",
                ("chat", world.ChatId));
            await RawAsync(world.TenantId, """UPDATE tenant."WhatsAppChats" SET "LastMessageAtUtc" = "LastMessageAtUtc" - interval '60 days' WHERE "Id" = @chat""",
                ("chat", world.ChatId));
        }
        await RawAsync(a.TenantId, """UPDATE engagement."WhatsAppConversationLinks" SET "LastMessageAtUtc" = "LastMessageAtUtc" - interval '60 days' WHERE "ChatId" = @chat""",
            ("chat", a.ChatId));
        await RawAsync(a.TenantId, """UPDATE engagement."WhatsAppLinkedMessages" SET "CreatedAtUtc" = "CreatedAtUtc" - interval '60 days' WHERE "BusinessTenantId" = @tenant""",
            ("tenant", a.TenantId));

        // Plataforma: una consulta de hace 100 días y otra de hoy, con su plazo de defecto (90).
        var (oldText, newText) = ("plataforma-vieja-" + Guid.NewGuid().ToString("N"), "plataforma-nueva-" + Guid.NewGuid().ToString("N"));
        PlatformWhatsAppChat? oldChat = null, newChat = null;
        await InScopeAsync(null, async (services, db) =>
        {
            var now = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
            var channel = await db.Set<WhatsAppChannel>().AsNoTracking().SingleAsync(value => value.PhoneNumberId == a.PhoneNumberId, Ct);
            (oldChat, newChat) = (Platform(channel.Id, now.AddDays(-100), oldText, db), Platform(channel.Id, now, newText, db));
        });

        var runner = host.Services.GetRequiredService<TenantJobRunner>();
        using var worker = new WhatsAppMessageRetentionWorker(host.Services.GetRequiredService<IServiceScopeFactory>(), runner,
            host.Services.GetRequiredService<TimeProvider>(), NullLogger<WhatsAppMessageRetentionWorker>.Instance);
        var cleared = 0;
        await runner.RunAsync([a.TenantId, b.TenantId], async (services, ct) => cleared += await worker.PurgeOrganizationAsync(services, ct), Ct);
        Assert.True(cleared >= a.Marks.Length + 3);
        await runner.RunAsync([a.TenantId, b.TenantId], async (services, ct) => Assert.Equal(0, await worker.PurgeOrganizationAsync(services, ct)), Ct);
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var retention = scope.ServiceProvider.GetRequiredService<IWhatsAppMessageRetentionService>();
            while ((await retention.ClearExpiredPlatformChatsAsync(Ct)).Value > 0) { }
        }

        Assert.All(await TextsAsync(host.Services, a.TenantId, inboundOnly: false), Assert.Null);
        Assert.Equal(b.Marks, await TextsAsync(host.Services, b.TenantId));
        Assert.Null(await PreviewAsync(host.Services, a.TenantId, a.ChatId));
        Assert.NotNull(await PreviewAsync(host.Services, b.TenantId, b.ChatId));
        await using (var scope = host.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(a.PersonalTenantId, TenantKind.Personal);
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Null(await db.Set<WhatsAppLinkedMessage>().Select(message => message.Text).SingleAsync(Ct));
            Assert.Null(await db.Set<WhatsAppConversationLink>().Select(link => link.LastMessagePreview).SingleAsync(Ct));
        }
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Null(await db.Set<PlatformWhatsAppChatMessage>().Where(message => message.ChatId == oldChat!.Id).Select(message => message.Text).SingleAsync(Ct));
            Assert.Null((await db.Set<PlatformWhatsAppChat>().AsNoTracking().SingleAsync(chat => chat.Id == oldChat!.Id, Ct)).LastMessagePreview);
            Assert.Equal(newText, await db.Set<PlatformWhatsAppChatMessage>().Where(message => message.ChatId == newChat!.Id).Select(message => message.Text).SingleAsync(Ct));
            Assert.Equal(newText, (await db.Set<PlatformWhatsAppChat>().AsNoTracking().SingleAsync(chat => chat.Id == newChat!.Id, Ct)).LastMessagePreview);
        }
    }

    private static PlatformWhatsAppChat Platform(Guid channelId, DateTime atUtc, string text, ApplicationDbContext db)
    {
        var chat = PlatformWhatsAppChat.Create(channelId, PhoneNumber.Create(WhatsAppAttentionJourney.NewPhone()).Value, atUtc);
        chat.RecordInbound(atUtc, text);
        db.AddRange(chat, PlatformWhatsAppChatMessage.Inbound(chat.Id, "wamid.272." + Guid.NewGuid().ToString("N"), text, null, atUtc));
        return chat;
    }

    private static async Task<string?> PreviewAsync(IServiceProvider root, Guid tenantId, Guid chatId)
    {
        await using var scope = root.CreateAsyncScope();
        using var enter = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
        return (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppChat>().AsNoTracking()
            .SingleAsync(chat => chat.Id == chatId, Ct)).LastMessagePreview;
    }

    private async Task<World> OrganizationAsync(WebApplicationFactory<Program> host, string label)
    {
        var client = WhatsAppAttentionJourney.Client(host);
        var owner = await AccountJourney.RegisterAsync(host, client, Ct);
        using var me = await client.GetFromJsonAsync<JsonDocument>("/api/me", Ct);
        var personal = me!.RootElement.GetProperty("activeTenantId").GetGuid();
        var tenant = await OrganizationJourney.StageAsync(factory, owner, "WhatsApp B8 " + label + " " + Guid.NewGuid().ToString("N")[..8], Ct);
        await WhatsAppAttentionJourney.EnableAsync(factory, tenant, Ct);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, tenant);
        var phoneNumberId = WhatsAppAttentionJourney.NewNumberId();
        (await WhatsAppAttentionJourney.ConnectAsync(client, phoneNumberId, Ct)).Dispose();
        var customer = WhatsAppAttentionJourney.NewPhone();
        string[] marks = [.. Enumerable.Range(1, 3).Select(index => $"{label}-{index}-{Guid.NewGuid():N}")];
        using var anonymous = WhatsAppAttentionJourney.Client(host);
        // Cada mensaje con su segundo de Meta, así el orden de lectura es el de escritura.
        var start = TimeProvider.System.GetUtcNow().AddMinutes(-1);
        for (var index = 0; index < marks.Length; index++)
            await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, customer, marks[index], Ct, start.AddSeconds(index));
        var chat = Chat(await JsonAsync(client.GetAsync($"{Customers}/conversations", Ct)), customer).GetProperty("id").GetGuid();
        return new World(label, client, owner.UserId, tenant, personal, phoneNumberId, customer, chat, marks);
    }

    private static IQueryable<string?> Ordered(IServiceProvider services) => services.GetRequiredService<ApplicationDbContext>()
        .Set<WhatsAppChatMessage>().AsNoTracking().Where(message => message.Direction == WhatsAppMessageDirection.Inbound)
        .OrderBy(message => message.CreatedAtUtc).ThenBy(message => message.Id).Select(message => message.Text);

    /// <summary>Con <c>ITenantScope.Enter</c>: lo que escribió el cliente, o todo el contenido del chat, notas incluidas.</summary>
    private static async Task<string?[]> TextsAsync(IServiceProvider root, Guid tenantId, bool inboundOnly = true)
    {
        await using var scope = root.CreateAsyncScope();
        using var enter = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
        var messages = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppChatMessage>().AsNoTracking();
        if (inboundOnly) messages = messages.Where(message => message.Direction == WhatsAppMessageDirection.Inbound);
        return await messages.OrderBy(message => message.CreatedAtUtc).ThenBy(message => message.Id).Select(message => message.Text)
            .ToArrayAsync(Ct);
    }

    private async Task InScopeAsync(Guid? tenantId, Func<IServiceProvider, ApplicationDbContext, Task> work, TenantKind? access = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        if (access is { } kind && tenantId is { } accessed) services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(accessed, kind);
        using var enter = access is null && tenantId is { } id ? services.GetRequiredService<ITenantScope>().Enter(id) : null;
        var saved = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async _ =>
        {
            await work(services, services.GetRequiredService<ApplicationDbContext>());
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(saved.IsSuccess);
    }

    private async Task<(Guid[] Ids, byte[][] Texts, byte[] Preview)> ContentAsync(Guid tenantId, Guid chatId)
    {
        await using var connection = await OpenAsync(tenantId);
        await using var messages = new NpgsqlCommand("""
            SELECT "Id", "Text" FROM tenant."WhatsAppChatMessages" WHERE "ChatId" = @chat ORDER BY "CreatedAtUtc", "Id"
            """, connection.Connection, connection.Transaction);
        messages.Parameters.AddWithValue("chat", chatId);
        var (ids, texts) = (new List<Guid>(), new List<byte[]>());
        await using (var reader = await messages.ExecuteReaderAsync(Ct))
            while (await reader.ReadAsync(Ct))
            {
                ids.Add(reader.GetGuid(0));
                texts.Add(reader.GetFieldValue<byte[]>(1));
            }
        await using var preview = new NpgsqlCommand("""SELECT "LastMessagePreview" FROM tenant."WhatsAppChats" WHERE "Id" = @chat""",
            connection.Connection, connection.Transaction);
        preview.Parameters.AddWithValue("chat", chatId);
        return ([.. ids], [.. texts], (byte[])(await preview.ExecuteScalarAsync(Ct))!);
    }

    private async Task<byte[]> ScalarAsync(Guid? tenantId, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await OpenAsync(tenantId);
        await using var command = new NpgsqlCommand(sql, connection.Connection, connection.Transaction);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        return (byte[])(await command.ExecuteScalarAsync(Ct))!;
    }

    private async Task RawAsync(Guid tenantId, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await OpenAsync(tenantId);
        await using var command = new NpgsqlCommand(sql, connection.Connection, connection.Transaction);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        Assert.True(await command.ExecuteNonQueryAsync(Ct) > 0);
        await connection.Transaction.CommitAsync(Ct);
    }

    /// <summary>mt_owner también respeta la RLS forzada: cada lectura cruda lleva su organización en el alcance.</summary>
    private async Task<RawConnection> OpenAsync(Guid? tenantId)
    {
        var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(Ct);
        var transaction = await connection.BeginTransactionAsync(Ct);
        await using var scope = new NpgsqlCommand("SELECT set_config('app.tenant_id', @tenant, true)", connection, transaction);
        scope.Parameters.AddWithValue("tenant", tenantId?.ToString("D") ?? string.Empty);
        await scope.ExecuteScalarAsync(Ct);
        return new RawConnection(connection, transaction);
    }

    private static bool Hides(byte[] cipher, IEnumerable<string> texts) =>
        cipher.Length > 0 && texts.All(text => cipher.AsSpan().IndexOf(Encoding.UTF8.GetBytes(text)) < 0);

    private static async Task<string[]> SearchAsync(HttpClient client, string search) =>
        [.. (await JsonAsync(client.GetAsync($"{Customers}/conversations?search={Uri.EscapeDataString(search)}", Ct)))
            .GetProperty("conversations").GetProperty("items").EnumerateArray().Select(row => row.GetProperty("displayNumber").GetString()!)];

    private static JsonElement Chat(JsonElement list, string waId) => list.GetProperty("conversations").GetProperty("items").EnumerateArray()
        .Single(row => row.GetProperty("displayNumber").GetString() == waId);

    private static string?[] Texts(JsonElement conversation) =>
        [.. conversation.GetProperty("messages").GetProperty("items").EnumerateArray()
            .Where(message => message.GetProperty("direction").GetString() == "Inbound")
            .OrderBy(message => message.GetProperty("createdAtUtc").GetDateTime()).Select(message => message.GetProperty("text").GetString())];

    private static async Task<JsonElement> JsonAsync(Task<HttpResponseMessage> request)
    {
        using var response = await request;
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return await WhatsAppAttentionJourney.JsonAsync(response, Ct);
    }

    private sealed record World(string Label, HttpClient Client, Guid OwnerUserId, Guid TenantId, Guid PersonalTenantId,
        string PhoneNumberId, string Customer, Guid ChatId, string[] Marks);

    private sealed class RawConnection(NpgsqlConnection connection, NpgsqlTransaction transaction) : IAsyncDisposable
    {
        public NpgsqlConnection Connection => connection;
        public NpgsqlTransaction Transaction => transaction;

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
