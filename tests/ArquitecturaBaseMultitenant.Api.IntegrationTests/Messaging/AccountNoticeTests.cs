using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Messaging;

[Collection(ApiTestGroup.Name)]
public sealed class AccountNoticeTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 1)]
    public async Task Notices_use_verified_contacts_without_duplicates_and_encrypt_owned_payloads(
        bool principalOnly, int expectedMessages)
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var work = services.GetRequiredService<IUnitOfWork>();
        var issuer = services.GetRequiredService<AccountNoticeIssuer>();
        var protector = services.GetRequiredService<IPayloadProtector>();
        var user = ApplicationUser.Create(null, "en-US", "America/Argentina/Buenos_Aires").Value;
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var firstEmail = Email.Create(user.Id.ToString("N") + "@example.test").Value;
        var first = LoginMethod.CreateEmail(user.Id, firstEmail);
        first.Verify(nowUtc);
        first.MakePrimary();
        var duplicate = LoginMethod.CreateGoogle(user.Id, user.Id.ToString("N"), firstEmail);
        duplicate.Verify(nowUtc);
        var second = LoginMethod.CreateEmail(user.Id, Email.Create("second-" + firstEmail.Value).Value);
        second.Verify(nowUtc);
        var pending = LoginMethod.CreateEmail(user.Id, Email.Create("pending-" + firstEmail.Value).Value);
        LoginMethod[] methods = [first, duplicate, second, pending];

        await work.ExecuteInTransactionAsync(async ct =>
        {
            context.Users.Add(user);
            context.LoginMethods.AddRange(methods);
            await issuer.EnqueueAsync(user.Id, methods, new AccountNotice.AccountDeleted(),
                user.Culture, principalOnly, ct);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        context.ChangeTracker.Clear();

        var messages = await context.OutboxMessages.AsNoTracking()
            .Where(message => message.UserId == user.Id).ToArrayAsync(Ct);
        Assert.Equal(expectedMessages, messages.Length);
        Assert.All(messages, message =>
        {
            Assert.DoesNotContain(firstEmail.Value, message.EncryptedPayload);
            Assert.Contains("Your account was deleted", protector.Unprotect(message.EncryptedPayload));
        });
        Assert.Contains(services.GetServices<IAccountNoticeChannel>(), channel => channel.Key == "email");
    }
}
