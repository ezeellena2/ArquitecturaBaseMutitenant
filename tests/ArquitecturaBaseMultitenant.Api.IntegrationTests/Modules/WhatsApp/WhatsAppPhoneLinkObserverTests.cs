using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

public sealed class WhatsAppPhoneLinkObserverTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public async Task First_phone_verification_before_method_add_is_atomic_and_does_not_reassign_anonymous_messages()
    {
        await using var host = WhatsAppPrivacyFixture.Host(factory);
        Guid user = Guid.Empty; Guid contactId = Guid.Empty; Guid anonymousId = Guid.Empty;
        var phone = PhoneNumber.Create("+54911" + Random.Shared.Next(10000000, 99999999)).Value;
        await WhatsAppPrivacyFixture.TransactionAsync(host.Services, async (services, ct) =>
        {
            user = (await services.GetRequiredService<IUserRepository>().CreateAsync(null, "es-AR", "UTC", ct)).Id;
            var contact = WhatsAppContact.Create(phone, WhatsAppPrivacyFixture.NowUtc); contactId = contact.Id;
            var anonymous = WhatsAppMessage.Create("privacy-number", Guid.NewGuid().ToString("N"), "inbound", WhatsAppMessageDirection.Inbound,
                WhatsAppMessageKind.Text, WhatsAppMessageStatus.Pending, WhatsAppPrivacyFixture.NowUtc, WhatsAppPrivacyFixture.NowUtc, contact.Id, "anonymous-text");
            anonymousId = anonymous.Id;
            services.GetRequiredService<ApplicationDbContext>().AddRange(contact, anonymous);
        }, Ct);
        for (var rollback = 1; rollback >= 0; rollback--)
        {
            await using var scope = host.Services.CreateAsyncScope(); var services = scope.ServiceProvider;
            var method = LoginMethod.CreatePhone(user, phone); method.Verify(WhatsAppPrivacyFixture.NowUtc);
            var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                await services.GetRequiredService<ILoginMethodRepository>().LockUserAsync(user, ct);
                await Assert.Single(services.GetServices<IPhoneLinkObserver>()).VerifiedAsync(user, method.Id, phone, ct);
                services.GetRequiredService<ILoginMethodRepository>().Add(method);
                return rollback == 1 ? Result.Failure(LoginMethodErrors.LastMethod) : Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
            Assert.Equal(rollback == 0, result.IsSuccess);
            await using var read = host.Services.CreateAsyncScope(); var db = read.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var contact = await db.Set<WhatsAppContact>().SingleAsync(row => row.Id == contactId, Ct);
            Assert.Equal(rollback == 1 ? null : (Guid?)user, contact.UserId);
            Assert.Equal(rollback == 1 ? null : (Guid?)method.Id, contact.MethodId);
            var anonymous = await db.Set<WhatsAppMessage>().SingleAsync(row => row.Id == anonymousId, Ct);
            Assert.Null(anonymous.OriginalUserId); Assert.Null(anonymous.OriginalMethodId); Assert.Equal("anonymous-text", anonymous.EncryptedText);
        }
    }
    [Fact]
    public async Task Removal_rolls_back_contact_and_text_then_commits_without_transferring_anonymous_history()
    {
        await using var host = WhatsAppPrivacyFixture.Host(factory);
        var seed = await WhatsAppPrivacyFixture.SeedAsync(host.Services, Ct);
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var observer = Assert.Single(services.GetServices<IPhoneLinkObserver>());
            var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                await services.GetRequiredService<ILoginMethodRepository>().LockUserAsync(seed.UserId, ct);
                await observer.RemovedAsync(seed.UserId, seed.MethodId, seed.Phone, ct);
                return Result.Failure(LoginMethodErrors.LastMethod);
            }, CommitPolicy.OnSuccess, Ct);
            Assert.True(result.IsFailure);
        }
        await using (var read = host.Services.CreateAsyncScope())
        {
            var db = read.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.True(await db.Set<WhatsAppContact>().AnyAsync(row => row.Id == seed.ContactId, Ct));
            Assert.NotNull((await db.Set<WhatsAppMessage>().SingleAsync(row => row.Id == seed.MessageId, Ct)).EncryptedText);
        }
        await WhatsAppPrivacyFixture.TransactionAsync(host.Services, async (services, ct) =>
        {
            await services.GetRequiredService<ILoginMethodRepository>().LockUserAsync(seed.UserId, ct);
            await Assert.Single(services.GetServices<IPhoneLinkObserver>()).RemovedAsync(seed.UserId, seed.MethodId, seed.Phone, ct);
        }, Ct);
        await using var final = host.Services.CreateAsyncScope();
        var database = final.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await database.Set<WhatsAppContact>().AnyAsync(row => row.Id == seed.ContactId, Ct));
        var owned = await database.Set<WhatsAppMessage>().SingleAsync(row => row.Id == seed.MessageId, Ct);
        Assert.Null(owned.EncryptedText); Assert.Null(owned.ContactId);
        Assert.Equal(seed.UserId, owned.OriginalUserId); Assert.Equal(seed.MethodId, owned.OriginalMethodId);
        var anonymous = await database.Set<WhatsAppMessage>().SingleAsync(row => row.Id == seed.AnonymousId, Ct);
        Assert.Null(anonymous.OriginalUserId); Assert.Null(anonymous.OriginalMethodId); Assert.NotNull(anonymous.EncryptedText);
    }

    [Fact]
    public async Task Verification_and_late_removal_cannot_steal_or_delete_another_owners_contact()
    {
        await using var host = WhatsAppPrivacyFixture.Host(factory);
        var original = await WhatsAppPrivacyFixture.SeedAsync(host.Services, Ct);
        Guid nextUser = Guid.Empty; Guid nextMethod = Guid.Empty;
        await WhatsAppPrivacyFixture.TransactionAsync(host.Services, async (services, ct) =>
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            nextUser = (await services.GetRequiredService<IUserRepository>().CreateAsync(null, "es-AR", "UTC", ct)).Id;
            var method = LoginMethod.CreatePhone(nextUser, original.Phone); method.Verify(WhatsAppPrivacyFixture.NowUtc);
            db.LoginMethods.Remove(await db.LoginMethods.SingleAsync(row => row.Id == original.MethodId, ct));
            db.LoginMethods.Add(method); nextMethod = method.Id;
        }, Ct);
        await WhatsAppPrivacyFixture.TransactionAsync(host.Services, async (services, ct) =>
        {
            await services.GetRequiredService<ILoginMethodRepository>().LockUserAsync(nextUser, ct);
            await Assert.Single(services.GetServices<IPhoneLinkObserver>()).VerifiedAsync(nextUser, nextMethod, original.Phone, ct);
        }, Ct);
        await using (var read = host.Services.CreateAsyncScope())
        {
            var contact = await read.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppContact>().SingleAsync(row => row.Id == original.ContactId, Ct);
            Assert.Equal(original.UserId, contact.UserId);
        }
        await WhatsAppPrivacyFixture.TransactionAsync(host.Services, async (services, ct) =>
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            (await db.Set<WhatsAppContact>().SingleAsync(row => row.Id == original.ContactId, ct)).Link(nextMethod, nextUser);
        }, Ct);
        await WhatsAppPrivacyFixture.TransactionAsync(host.Services, async (services, ct) =>
        {
            await services.GetRequiredService<ILoginMethodRepository>().LockUserAsync(original.UserId, ct);
            await Assert.Single(services.GetServices<IPhoneLinkObserver>()).RemovedAsync(original.UserId, original.MethodId, original.Phone, ct);
        }, Ct);
        await using var final = host.Services.CreateAsyncScope();
        var database = final.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(nextUser, (await database.Set<WhatsAppContact>().SingleAsync(row => row.Id == original.ContactId, Ct)).UserId);
        Assert.Equal(original.UserId, (await database.Set<WhatsAppMessage>().SingleAsync(row => row.Id == original.MessageId, Ct)).OriginalUserId);
        Assert.Null((await database.Set<WhatsAppMessage>().SingleAsync(row => row.Id == original.AnonymousId, Ct)).OriginalUserId);
    }

    [Fact]
    public async Task Removal_deletes_only_the_conversation_links_of_the_removed_phone_and_rolls_back_atomically()
    {
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        using var owner = WhatsAppAttentionJourney.Client(host);
        var (_, businessTenantId) = await WhatsAppAttentionJourney.OrganizationAsync(factory, host, owner, Ct);
        var phoneNumberId = WhatsAppAttentionJourney.NewNumberId();
        using var connected = await WhatsAppAttentionJourney.ConnectAsync(owner, phoneNumberId, Ct);
        var numberId = (await WhatsAppAttentionJourney.JsonAsync(connected, Ct)).GetProperty("id").GetGuid();
        using (var bot = await owner.PutAsJsonAsync($"/api/organization/whatsapp/numbers/{numberId}/bot", new
        {
            welcome = "Bienvenidos",
            outOfHours = "Cerrado",
            schedule = new
            {
                timeZoneId = "America/Argentina/Buenos_Aires",
                days = Enum.GetValues<DayOfWeek>().Select(day => new { day = day.ToString(), from = "00:00:00", to = "23:59:59" }).ToArray(),
            },
            menu = Array.Empty<object>(),
            askPromotions = false,
            offerAccountLink = true,
            employeesTeamId = (Guid?)null,
            version = 0u,
        }, Ct)) Assert.Equal(HttpStatusCode.OK, bot.StatusCode);

        using var person = WhatsAppAttentionJourney.Client(host);
        var account = await AccountJourney.RegisterAsync(host, person, Ct);
        var removedPhone = PhoneNumber.Create(WhatsAppAttentionJourney.NewPhone()).Value;
        var keptPhone = PhoneNumber.Create(WhatsAppAttentionJourney.NewPhone()).Value;
        Guid removedMethodId = Guid.Empty;
        await WhatsAppPrivacyFixture.TransactionAsync(host.Services, (services, _) =>
        {
            var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
            var removedMethod = LoginMethod.CreatePhone(account.UserId, removedPhone); removedMethod.Verify(nowUtc);
            var keptMethod = LoginMethod.CreatePhone(account.UserId, keptPhone); keptMethod.Verify(nowUtc);
            services.GetRequiredService<ILoginMethodRepository>().Add(removedMethod);
            services.GetRequiredService<ILoginMethodRepository>().Add(keptMethod);
            removedMethodId = removedMethod.Id;
            return Task.CompletedTask;
        }, Ct);

        using var anonymous = WhatsAppAttentionJourney.Client(host);
        foreach (var phone in new[] { removedPhone.Value, keptPhone.Value })
        {
            await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, phone, "Hola", Ct);
            await LinkChoiceAsync(host, anonymous, phoneNumberId, phone);
        }
        Assert.Equal(2, (await JsonAsync(person.GetAsync("/api/me/whatsapp/conversations", Ct)))
            .GetProperty("totalCount").GetInt32());

        // Rollback sin espacio activo: los dos vínculos quedan intactos.
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var observer = Assert.Single(services.GetServices<IPhoneLinkObserver>());
            var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                await services.GetRequiredService<ILoginMethodRepository>().LockUserAsync(account.UserId, ct);
                await observer.RemovedAsync(account.UserId, removedMethodId, removedPhone, ct);
                return Result.Failure(LoginMethodErrors.LastMethod);
            }, CommitPolicy.OnSuccess, Ct);
            Assert.True(result.IsFailure);
        }
        Assert.Equal(2, (await JsonAsync(person.GetAsync("/api/me/whatsapp/conversations", Ct)))
            .GetProperty("totalCount").GetInt32());

        // Commit sin espacio activo: se borra solo el vínculo del teléfono quitado; el del otro teléfono sigue.
        await WhatsAppPrivacyFixture.TransactionAsync(host.Services, async (services, ct) =>
        {
            await services.GetRequiredService<ILoginMethodRepository>().LockUserAsync(account.UserId, ct);
            await Assert.Single(services.GetServices<IPhoneLinkObserver>()).RemovedAsync(account.UserId, removedMethodId, removedPhone, ct);
        }, Ct);
        Assert.Equal(1, (await JsonAsync(person.GetAsync("/api/me/whatsapp/conversations", Ct)))
            .GetProperty("totalCount").GetInt32());
        var conversations = (await JsonAsync(owner.GetAsync("/api/organization/whatsapp/customers/conversations", Ct)))
            .GetProperty("conversations").GetProperty("items").EnumerateArray().ToArray();
        Assert.False(conversations.Single(row => row.GetProperty("displayNumber").GetString() == removedPhone.Value)
            .GetProperty("linkedToAccount").GetBoolean());
        Assert.True(conversations.Single(row => row.GetProperty("displayNumber").GetString() == keptPhone.Value)
            .GetProperty("linkedToAccount").GetBoolean());

        // El borrado por SQL deja en la persona y en la organización la misma entrada de auditoría que dejaría el interceptor.
        Guid personalTenantId;
        await using (var lookup = host.Services.CreateAsyncScope())
            personalTenantId = (await lookup.ServiceProvider.GetRequiredService<IUserTenantAccessReader>().ListForUserAsync(account.UserId, Ct))
                .Single(row => row.Kind == TenantKind.Personal).TenantId;
        foreach (var tenantId in new[] { businessTenantId, personalTenantId })
        {
            await using var connection = await RuntimeRoleConnection.OpenAsync(factory.RuntimeConnectionString, tenantId, Ct);
            await using var command = new NpgsqlCommand("""
                SELECT "Changes"::text FROM tenant."AuditEntries"
                WHERE "EntityType" = 'WhatsAppConversationLink' AND "Action" = 'Deleted' AND "TenantId" = @tenant
                """, connection);
            command.Parameters.AddWithValue("tenant", tenantId);
            var changes = (string)(await command.ExecuteScalarAsync(Ct))!;
            using var diff = JsonDocument.Parse(changes);
            Assert.Equal(account.UserId, diff.RootElement.GetProperty("AccountId").GetProperty("old").GetGuid());
            Assert.Equal(JsonValueKind.Null, diff.RootElement.GetProperty("AccountId").GetProperty("new").ValueKind);
            Assert.False(diff.RootElement.TryGetProperty("LastMessagePreview", out _));
        }
    }

    private static async Task<JsonElement> JsonAsync(Task<HttpResponseMessage> request)
    {
        using var response = await request;
        return await WhatsAppAttentionJourney.JsonAsync(response, Ct);
    }

    private static async Task LinkChoiceAsync(WebApplicationFactory<Program> host, HttpClient client, string phoneNumberId, string from)
    {
        var body = JsonSerializer.Serialize(new
        {
            @object = "whatsapp_business_account",
            entry = new[] { new { id = "waba-" + phoneNumberId, changes = new[] { new { field = "messages", value = new
            {
                messaging_product = "whatsapp",
                metadata = new { display_phone_number = "5491100000000", phone_number_id = phoneNumberId },
                messages = new[] { new
                {
                    from = from.TrimStart('+'), id = "wamid.obs.link." + Guid.NewGuid().ToString("N"), type = "interactive",
                    interactive = new { type = "button_reply", button_reply = new { id = BotButtons.LinkAccount, title = "Vincular mi cuenta" } },
                    timestamp = TimeProvider.System.GetUtcNow().ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
                } },
            } } } } },
        });
        using var received = await WhatsAppAttentionJourney.WebhookAsync(client, body, Ct);
        Assert.Equal(HttpStatusCode.OK, received.StatusCode);
        await host.Services.GetServices<IHostedService>().OfType<Infrastructure.Modules.WhatsApp.Inbound.WhatsAppInboundProcessor>().Single()
            .ProcessPendingAsync(Ct);
    }
}

internal static class WhatsAppPrivacyFixture
{
    internal static readonly DateTime NowUtc = new(2030, 10, 8, 12, 0, 0, DateTimeKind.Utc);
    internal static WebApplicationFactory<Program> Host(ApiFactory factory, FakeTimeProvider? clock = null, bool enabled = false) =>
        factory.WithWebHostBuilder(builder =>
        {
            if (enabled) { builder.UseSetting("WhatsApp:PhoneNumberId", "privacy-number"); builder.UseSetting("WhatsApp:AccessToken", "fixture-token"); }
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHostedService>(); services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(clock ?? new FakeTimeProvider(new DateTimeOffset(NowUtc)));
            });
        });
    internal sealed record Seed(Guid UserId, Guid MethodId, PhoneNumber Phone, Guid ContactId, Guid MessageId, Guid AnonymousId);
    internal static async Task<Seed> SeedAsync(IServiceProvider provider, CancellationToken ct)
    {
        Seed? seed = null;
        await TransactionAsync(provider, async (services, c) =>
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            var user = (await services.GetRequiredService<IUserRepository>().CreateAsync(null, "es-AR", "UTC", c)).Id;
            var phone = PhoneNumber.Create("+54911" + Random.Shared.Next(10000000, 99999999)).Value;
            var method = LoginMethod.CreatePhone(user, phone); method.Verify(NowUtc); method.MakePrimary(); db.LoginMethods.Add(method);
            var contact = WhatsAppContact.Create(phone, NowUtc); contact.Link(method.Id, user); db.Set<WhatsAppContact>().Add(contact);
            var message = WhatsAppMessage.Create("privacy-number", Guid.NewGuid().ToString("N"), "inbound", WhatsAppMessageDirection.Inbound,
                WhatsAppMessageKind.Text, WhatsAppMessageStatus.Pending, NowUtc, NowUtc, contact.Id, "encrypted-owned", originalUserId: user, originalMethodId: method.Id);
            var anonymous = WhatsAppMessage.Create("privacy-number", Guid.NewGuid().ToString("N"), "inbound", WhatsAppMessageDirection.Inbound,
                WhatsAppMessageKind.Text, WhatsAppMessageStatus.Pending, NowUtc, NowUtc, contact.Id, "encrypted-anonymous");
            db.Set<WhatsAppMessage>().AddRange(message, anonymous);
            seed = new(user, method.Id, phone, contact.Id, message.Id, anonymous.Id);
        }, ct);
        return seed!;
    }
    internal static async Task TransactionAsync(IServiceProvider services, Func<IServiceProvider, CancellationToken, Task> work, CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async c =>
        { await work(scope.ServiceProvider, c); return Result.Success(); }, CommitPolicy.OnSuccess, ct);
        Assert.True(result.IsSuccess);
    }
}
