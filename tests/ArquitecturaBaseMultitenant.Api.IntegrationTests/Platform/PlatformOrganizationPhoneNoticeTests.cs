using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.PublicSite;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Platform;

[Collection(ApiTestGroup.Name)]
public sealed partial class PlatformOrganizationPhoneNoticeTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Theory]
    [InlineData("approve", "Aprobamos")]
    [InlineData("reject", "No aprobamos")]
    [InlineData("unpublish", "despublicó")]
    [InlineData("allow-publish", "permitió")]
    public Task Real_actions_notify_email_backup_without_phone_channel_and_replay_does_not_duplicate(string action, string expected) =>
        AssertNoticeAsync(action, expected, "email");

    private async Task AssertNoticeAsync(string action, string expected, string channel,
        Action<Microsoft.AspNetCore.Hosting.IWebHostBuilder>? configureHost = null,
        Action<string, PhoneNumber, Tenant>? verifyPhone = null)
    {
        using var host = factory.WithWebHostBuilder(builder =>
        {
            configureHost?.Invoke(builder);
        });
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var operatorAccount = await AccountJourney.RegisterAsync(factory, client, Ct);
        await PlatformOperators.GrantAsync(host.Services, operatorAccount.UserId, PlatformOperators.All, Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        var owner = ApplicationUser.Create(null, "es-AR", "America/Argentina/Buenos_Aires").Value;
        var phone = PhoneNumber.Create("+54911" + RandomNumberGenerator.GetInt32(10000000, 99999999).ToString(CultureInfo.InvariantCulture)).Value;
        var email = Email.Create(Guid.NewGuid().ToString("N") + "@example.test").Value;
        var moderation = action is "unpublish" or "allow-publish";
        var tenant = Tenant.CreateBusiness("Aviso Phone " + Guid.NewGuid().ToString("N"), !moderation);
        if (moderation) Assert.True(tenant.Activate().IsSuccess);
        await using (var setup = host.Services.CreateAsyncScope())
        {
            var services = setup.ServiceProvider;
            var db = services.GetRequiredService<ApplicationDbContext>();
            var clock = services.GetRequiredService<TimeProvider>();
            tenant.RecordRegistration(owner.Id, TenantRegistrationSource.SelfService, clock.GetUtcNow().UtcDateTime);
            var primary = LoginMethod.CreatePhone(owner.Id, phone);
            Assert.True(primary.Verify(clock.GetUtcNow().UtcDateTime).IsSuccess);
            Assert.True(primary.MakePrimary().IsSuccess);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                db.Users.Add(owner); db.LoginMethods.Add(primary);
                if (verifyPhone is null)
                {
                    var backup = LoginMethod.CreateEmail(owner.Id, email);
                    Assert.True(backup.Verify(clock.GetUtcNow().UtcDateTime).IsSuccess); db.LoginMethods.Add(backup);
                }
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
            services.GetRequiredService<ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request.ITenantAccessInitializer>()
                .SetFromAccess(tenant.Id, TenantKind.Business);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                services.GetRequiredService<TenantSpaceProvisioner>().Stage(tenant,
                    TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"), [owner.Id], owner.Id);
                if (moderation)
                {
                    var page = PublicPage.Create();
                    Assert.True(page.UpdateContent("Página Phone", null, null, null, null, null).IsSuccess);
                    Assert.True(page.Publish(clock.GetUtcNow().UtcDateTime).IsSuccess);
                    if (action == "allow-publish") Assert.True(page.ApplyModerationBlock("Previous internal reason", operatorAccount.UserId, clock.GetUtcNow().UtcDateTime).IsSuccess);
                    services.GetRequiredService<IPublicPageRepository>().Add(page);
                }
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }
        var path = $"/api/platform/tenants/{tenant.Id:D}" + (moderation ? "/public-site" : "");
        using var read = await client.GetAsync(path, Ct); Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        using var json = JsonDocument.Parse(await read.Content.ReadAsStringAsync(Ct));
        var body = new { version = json.RootElement.GetProperty("version").GetUInt32(), reason = "INTERNAL-ONLY-REASON" };
        var key = Guid.NewGuid().ToString("D");
        using var first = await PostAsync(client, path + "/" + action, body, key);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var replay = await PostAsync(client, path + "/" + action, body, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotent-Replayed")));
        await using var verify = host.Services.CreateAsyncScope();
        var rows = await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutboxMessages.AsNoTracking()
            .Where(value => value.UserId == owner.Id).ToArrayAsync(Ct);
        var message = Assert.Single(rows);
        Assert.Equal(channel, message.Channel);
        var payload = verify.ServiceProvider.GetRequiredService<IPayloadProtector>().Unprotect(message.EncryptedPayload);
        Assert.DoesNotContain("INTERNAL-ONLY-REASON", payload);
        if (verifyPhone is not null) verifyPhone(payload, phone, tenant);
        else
        {
            var outbound = JsonSerializer.Deserialize<EmailMessage>(payload)!;
            Assert.Equal(email.Value, outbound.To); Assert.Contains(expected, outbound.TextBody);
            Assert.Contains(tenant.Name, outbound.TextBody);
        }
    }
    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object body, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key); return await client.SendAsync(request, Ct);
    }
}
