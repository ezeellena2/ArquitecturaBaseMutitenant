using ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

/// <summary>
/// Revisión adversarial de la B18 (272): un contenido que el anillo no abre (una base restaurada sin el certificado, claves
/// perdidas o un cifrado copiado de otra organización) se lee vacío (ADR 0065). La purga de los chats lo tiene que vaciar en la
/// base igual: si no, el lote lo vuelve a tomar para siempre y el worker, que repite mientras el lote borró algo, no termina.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class WhatsAppChatRetentionUnreadableAdversarialTests(ApiFactory factory)
{
    private const string Customers = "/api/organization/whatsapp/customers";
    private static readonly byte[] Unreadable = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08];
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_expired_organization_chat_whose_content_does_not_open_is_cleared_in_the_database_and_the_next_batch_is_empty()
    {
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        var (tenantId, chatId, _) = await OrganizationAsync(host);
        try
        {
            await RawAsync(tenantId, """
                UPDATE tenant."WhatsAppChatMessages" SET "Text" = @cipher, "CreatedAtUtc" = "CreatedAtUtc" - interval '100 days' WHERE "ChatId" = @chat;
                UPDATE tenant."WhatsAppChats" SET "LastMessagePreview" = @cipher, "LastMessageAtUtc" = "LastMessageAtUtc" - interval '100 days' WHERE "Id" = @chat;
                """, ("cipher", Unreadable), ("chat", chatId));

            await using var scope = host.Services.CreateAsyncScope();
            using var enter = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
            var retention = scope.ServiceProvider.GetRequiredService<IWhatsAppMessageRetentionService>();
            var first = await retention.ClearExpiredOrganizationChatsAsync(Ct);
            Assert.True(first.IsSuccess);
            Assert.True(first.Value > 0);

            // Lo vencido quedó vacío en la base, no solo en lo que leyó EF.
            Assert.Equal(0, await CountAsync(tenantId, """
                SELECT count(*)::int FROM tenant."WhatsAppChatMessages" WHERE "ChatId" = @chat AND "Text" IS NOT NULL
                """, ("chat", chatId)));
            Assert.Equal(0, await CountAsync(tenantId, """
                SELECT count(*)::int FROM tenant."WhatsAppChats" WHERE "Id" = @chat AND "LastMessagePreview" IS NOT NULL
                """, ("chat", chatId)));
            // El worker repite el lote mientras borró algo: el siguiente tiene que venir vacío.
            Assert.Equal(0, (await retention.ClearExpiredOrganizationChatsAsync(Ct)).Value);
        }
        finally
        {
            await RawAsync(tenantId, """
                UPDATE tenant."WhatsAppChatMessages" SET "Text" = NULL WHERE "ChatId" = @chat;
                UPDATE tenant."WhatsAppChats" SET "LastMessagePreview" = NULL WHERE "Id" = @chat;
                """, ("chat", chatId));
        }
    }

    [Fact]
    public async Task An_expired_platform_chat_whose_content_does_not_open_is_cleared_in_the_database_and_the_next_batch_is_empty()
    {
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        var (_, _, phoneNumberId) = await OrganizationAsync(host);
        Guid platformChatId = Guid.Empty;
        try
        {
            await InPlatformAsync(async (services, db) =>
            {
                var now = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
                var channel = await db.Set<WhatsAppChannel>().AsNoTracking().SingleAsync(value => value.PhoneNumberId == phoneNumberId, Ct);
                var chat = PlatformWhatsAppChat.Create(channel.Id, PhoneNumber.Create(WhatsAppAttentionJourney.NewPhone()).Value, now);
                chat.RecordInbound(now, "plataforma");
                db.AddRange(chat, PlatformWhatsAppChatMessage.Inbound(chat.Id, "wamid.b18adv." + Guid.NewGuid().ToString("N"),
                    "plataforma", null, now));
                platformChatId = chat.Id;
            });
            await RawAsync(null, """
                UPDATE platform."PlatformWhatsAppChatMessages" SET "Text" = @cipher, "CreatedAtUtc" = "CreatedAtUtc" - interval '100 days' WHERE "ChatId" = @chat;
                UPDATE platform."PlatformWhatsAppChats" SET "LastMessagePreview" = @cipher, "LastMessageAtUtc" = "LastMessageAtUtc" - interval '100 days' WHERE "Id" = @chat;
                """, ("cipher", Unreadable), ("chat", platformChatId));

            await using var scope = host.Services.CreateAsyncScope();
            var retention = scope.ServiceProvider.GetRequiredService<IWhatsAppMessageRetentionService>();
            // El worker repite el lote de Plataforma mientras borró algo, antes de pasar a las organizaciones.
            for (var batch = 0; batch < 20 && (await retention.ClearExpiredPlatformChatsAsync(Ct)).Value > 0; batch++) { }

            Assert.Equal(0, await CountAsync(null, """
                SELECT count(*)::int FROM platform."PlatformWhatsAppChatMessages" WHERE "ChatId" = @chat AND "Text" IS NOT NULL
                """, ("chat", platformChatId)));
            Assert.Equal(0, (await retention.ClearExpiredPlatformChatsAsync(Ct)).Value);
        }
        finally
        {
            if (platformChatId != Guid.Empty)
                await RawAsync(null, """
                    UPDATE platform."PlatformWhatsAppChatMessages" SET "Text" = NULL WHERE "ChatId" = @chat;
                    UPDATE platform."PlatformWhatsAppChats" SET "LastMessagePreview" = NULL WHERE "Id" = @chat;
                    """, ("chat", platformChatId));
        }
    }

    private async Task<(Guid TenantId, Guid ChatId, string PhoneNumberId)> OrganizationAsync(WebApplicationFactory<Program> host)
    {
        var client = WhatsAppAttentionJourney.Client(host);
        var owner = await AccountJourney.RegisterAsync(host, client, Ct);
        var tenant = await OrganizationJourney.StageAsync(factory, owner, "WhatsApp B18 adv " + Guid.NewGuid().ToString("N")[..8], Ct);
        await WhatsAppAttentionJourney.EnableAsync(factory, tenant, Ct);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, tenant);
        var phoneNumberId = WhatsAppAttentionJourney.NewNumberId();
        (await WhatsAppAttentionJourney.ConnectAsync(client, phoneNumberId, Ct)).Dispose();
        var customer = WhatsAppAttentionJourney.NewPhone();
        using var anonymous = WhatsAppAttentionJourney.Client(host);
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, customer, "hola-" + Guid.NewGuid().ToString("N"), Ct);
        using var response = await client.GetAsync($"{Customers}/conversations", Ct);
        Assert.True(response.IsSuccessStatusCode);
        var list = await WhatsAppAttentionJourney.JsonAsync(response, Ct);
        var chat = list.GetProperty("conversations").GetProperty("items").EnumerateArray()
            .Single(row => row.GetProperty("displayNumber").GetString() == customer).GetProperty("id").GetGuid();
        return (tenant, chat, phoneNumberId);
    }

    private async Task InPlatformAsync(Func<IServiceProvider, ApplicationDbContext, Task> work)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var saved = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async _ =>
        {
            await work(services, services.GetRequiredService<ApplicationDbContext>());
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(saved.IsSuccess);
    }

    private async Task<int> CountAsync(Guid? tenantId, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await OpenAsync(tenantId);
        await using var command = new NpgsqlCommand(sql, connection.Connection, connection.Transaction);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        return (int)(await command.ExecuteScalarAsync(Ct))!;
    }

    private async Task RawAsync(Guid? tenantId, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await OpenAsync(tenantId);
        await using var command = new NpgsqlCommand(sql, connection.Connection, connection.Transaction);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync(Ct);
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
