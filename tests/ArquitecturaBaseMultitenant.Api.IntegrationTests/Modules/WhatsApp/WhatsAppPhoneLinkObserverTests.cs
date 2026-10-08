using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
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
    public async Task Removal_rolls_back_contact_text_and_links_then_commits_without_transferring_anonymous_history()
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
            Assert.Null((await db.Set<LoginLink>().SingleAsync(row => row.Id == seed.LinkId, Ct)).InvalidatedAtUtc);
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
        Assert.NotNull((await database.Set<LoginLink>().SingleAsync(row => row.Id == seed.LinkId, Ct)).InvalidatedAtUtc);
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
    internal sealed record Seed(Guid UserId, Guid MethodId, PhoneNumber Phone, Guid ContactId, Guid MessageId, Guid AnonymousId, Guid LinkId);
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
            var link = LoginLink.Issue(user, method.Id, Guid.NewGuid().ToString("N"), NowUtc); db.Set<LoginLink>().Add(link);
            seed = new(user, method.Id, phone, contact.Id, message.Id, anonymous.Id, link.Id);
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
