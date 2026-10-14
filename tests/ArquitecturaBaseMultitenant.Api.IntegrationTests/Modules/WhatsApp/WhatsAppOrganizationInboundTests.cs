using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Inbound;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

/// <summary>
/// B2 de la E11: la entrada de cada número va a su organización (por el identificador de Meta, nunca por el remitente), un
/// miembro activo con ese teléfono va a Internos y uno que dejó de serlo, a Clientes; con el módulo apagado se conserva.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class WhatsAppOrganizationInboundTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Two_organizations_with_their_own_numbers_never_see_each_other_messages()
    {
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        using var first = WhatsAppAttentionJourney.Client(host);
        using var second = WhatsAppAttentionJourney.Client(host);
        var (_, tenantA) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, first, Ct);
        var (_, tenantB) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, second, Ct);
        var numberA = WhatsAppAttentionJourney.NewNumberId(); var numberB = WhatsAppAttentionJourney.NewNumberId();
        (await WhatsAppAttentionJourney.ConnectAsync(first, numberA, Ct)).Dispose();
        (await WhatsAppAttentionJourney.ConnectAsync(second, numberB, Ct)).Dispose();
        var customer = NewPhone();
        using var anonymous = WhatsAppAttentionJourney.Client(host);
        using var received = await WhatsAppAttentionJourney.WebhookAsync(anonymous, Inbound(
            (numberA, customer, "wamid.a." + Guid.NewGuid().ToString("N"), "Hola A"),
            (numberB, customer, "wamid.b." + Guid.NewGuid().ToString("N"), "Hola B")), Ct);
        Assert.Equal(HttpStatusCode.OK, received.StatusCode);
        await ProcessAsync(host);

        var a = await ChatsAsync(host, tenantA);
        var b = await ChatsAsync(host, tenantB);
        Assert.Equal(["Hola A"], a.Select(value => value.Text));
        Assert.Equal(["Hola B"], b.Select(value => value.Text));
        Assert.All(a.Concat(b), value => Assert.Equal(WhatsAppInbox.Customers, value.Inbox));
        await using var scope = host.Services.CreateAsyncScope();
        var global = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppMessage>().AsNoTracking()
            .Where(value => value.Wamid.StartsWith("wamid.a.") || value.Wamid.StartsWith("wamid.b.")).ToArrayAsync(Ct);
        Assert.Contains(global, value => value.ProcessedAtUtc is not null);
        Assert.All(global.Where(value => value.ProcessedAtUtc is not null), value => Assert.Null(value.EncryptedText));
    }

    [Fact]
    public async Task An_active_member_writes_to_internal_and_after_leaving_to_customers()
    {
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        using var client = WhatsAppAttentionJourney.Client(host);
        var (_, tenant) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, client, Ct);
        var number = WhatsAppAttentionJourney.NewNumberId();
        (await WhatsAppAttentionJourney.ConnectAsync(client, number, Ct)).Dispose();
        var phone = NewPhone();
        var employee = await VerifiedPhoneAccountAsync(phone);
        await OrganizationJourney.AddActiveMemberAsync(factory, tenant, employee, Ct);
        using var anonymous = WhatsAppAttentionJourney.Client(host);

        (await WhatsAppAttentionJourney.WebhookAsync(anonymous, Inbound((number, phone, "wamid.e." + Guid.NewGuid().ToString("N"), "Soy del equipo")), Ct)).Dispose();
        await ProcessAsync(host);
        var internalChat = Assert.Single(await ChatsAsync(host, tenant));
        Assert.Equal((WhatsAppInbox.Internal, employee), (internalChat.Inbox, internalChat.MemberUserId));

        await DeactivateAsync(tenant, employee);
        (await WhatsAppAttentionJourney.WebhookAsync(anonymous, Inbound((number, phone, "wamid.e." + Guid.NewGuid().ToString("N"), "Ya no trabajo acá")), Ct)).Dispose();
        await ProcessAsync(host);
        var customerChat = Assert.Single(await ChatsAsync(host, tenant), value => value.Inbox == WhatsAppInbox.Customers);
        Assert.Null(customerChat.MemberUserId);
        Assert.NotNull(customerChat.CustomerId);
        Assert.Equal("Ya no trabajo acá", customerChat.Text);
    }

    [Fact]
    public async Task With_the_module_off_the_entry_is_kept_without_processing_or_spending_attempts()
    {
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        using var client = WhatsAppAttentionJourney.Client(host);
        var (_, tenant) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, client, Ct);
        var number = WhatsAppAttentionJourney.NewNumberId();
        (await WhatsAppAttentionJourney.ConnectAsync(client, number, Ct)).Dispose();
        await DisableAsync(host, tenant);
        var wamid = "wamid.off." + Guid.NewGuid().ToString("N");
        using var anonymous = WhatsAppAttentionJourney.Client(host);
        (await WhatsAppAttentionJourney.WebhookAsync(anonymous, Inbound((number, NewPhone(), wamid, "Hola")), Ct)).Dispose();
        await ProcessAsync(host);

        Assert.Empty(await ChatsAsync(host, tenant));
        await using var scope = host.Services.CreateAsyncScope();
        var kept = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppMessage>().AsNoTracking()
            .SingleAsync(value => value.Wamid == wamid, Ct);
        Assert.Null(kept.ProcessedAtUtc);
        Assert.NotNull(kept.EncryptedText);
        Assert.Equal(0, kept.Attempts);
        Assert.True(kept.NextAttemptAtUtc > TimeProvider.System.GetUtcNow().UtcDateTime);
    }

    private static async Task ProcessAsync(WebApplicationFactory<Program> host) =>
        await host.Services.GetServices<IHostedService>().OfType<WhatsAppInboundProcessor>().Single().ProcessPendingAsync(Ct);

    private sealed record ChatRow(WhatsAppInbox Inbox, Guid? MemberUserId, Guid? CustomerId, string? Text);

    /// <summary>Lo que ve la organización: entra a su alcance, como un trabajo, y lee con RLS.</summary>
    private static async Task<IReadOnlyList<ChatRow>> ChatsAsync(WebApplicationFactory<Program> host, Guid tenantId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        using var tenantScope = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await (from chat in db.Set<WhatsAppChat>().AsNoTracking()
                      from message in db.Set<WhatsAppChatMessage>().AsNoTracking().Where(value => value.ChatId == chat.Id)
                      orderby message.CreatedAtUtc
                      select new ChatRow(chat.Inbox, chat.MemberUserId, chat.CustomerId, message.Text)).ToArrayAsync(Ct);
    }

    private async Task<Guid> VerifiedPhoneAccountAsync(string phone)
    {
        var user = Guid.Empty;
        await TransactionAsync(async (provider, ct) =>
        {
            user = (await provider.GetRequiredService<IUserRepository>().CreateAsync("Empleada", "es-AR", "America/Argentina/Buenos_Aires", ct)).Id;
            var method = LoginMethod.CreatePhone(user, PhoneNumber.Create(phone).Value);
            method.Verify(provider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
            provider.GetRequiredService<ILoginMethodRepository>().Add(method);
        });
        return user;
    }

    private async Task DeactivateAsync(Guid tenantId, Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        using var tenantScope = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var member = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Members.SingleAsync(value => value.UserId == userId, ct);
            Assert.True(member.Deactivate().IsSuccess);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
    }

    /// <summary>Apaga el módulo e invalida la caché del host que procesa (cada host tiene la suya).</summary>
    private static async Task DisableAsync(WebApplicationFactory<Program> host, Guid tenantId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var feature = await services.GetRequiredService<ApplicationDbContext>().TenantFeatures
                .SingleAsync(value => value.TenantId == tenantId && value.FeatureKey == WhatsAppFeature.Key, ct);
            Assert.True(feature.Update(false, feature.Source, null).IsSuccess);
            services.GetRequiredService<ISecurityEventRepository>().Add(Domain.Auditing.SecurityEvent.ForTenantFeatures(
                Domain.Auditing.AuditActorKind.System, Guid.Empty, tenantId, "WhatsApp attention fixture", services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        await services.GetRequiredService<Application.Interfaces.Integrations.Caching.ITenantFeatureCache>().InvalidateAsync(tenantId, Ct);
    }

    private async Task TransactionAsync(Func<IServiceProvider, CancellationToken, Task> work)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        { await work(scope.ServiceProvider, ct); return Result.Success(); }, CommitPolicy.OnSuccess, Ct);
    }

    private static string NewPhone() => "+54911" + System.Security.Cryptography.RandomNumberGenerator.GetInt32(10_000_000, 99_999_999)
        .ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string Inbound(params (string PhoneNumberId, string From, string Wamid, string Text)[] messages) => JsonSerializer.Serialize(new
    {
        @object = "whatsapp_business_account",
        entry = messages.GroupBy(value => value.PhoneNumberId).Select(group => new
        {
            id = "waba-" + group.Key,
            changes = new[] { new { field = "messages", value = new
            {
                messaging_product = "whatsapp",
                metadata = new { display_phone_number = "5491100000000", phone_number_id = group.Key },
                messages = group.Select(value => new
                {
                    from = value.From.TrimStart('+'), id = value.Wamid, type = "text", text = new { body = value.Text },
                    timestamp = TimeProvider.System.GetUtcNow().ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
                }).ToArray(),
            } } },
        }).ToArray(),
    });
}
