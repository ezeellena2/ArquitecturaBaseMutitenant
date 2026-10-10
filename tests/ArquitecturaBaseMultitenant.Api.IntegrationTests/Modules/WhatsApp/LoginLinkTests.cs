using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

public sealed partial class LoginLinkTests
{
    [Fact]
    public async Task Verified_phone_survives_country_reduction_and_locked_account_gets_no_cookie()
    {
        await using var app = Host(); using var setup = app.CreateClient(); var (user, _, token) = await PrepareAsync(app, setup);
        await using var reduced = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("WhatsApp:PhoneNumberId", "fixture-number"); builder.UseSetting("WhatsApp:AccessToken", "fixture-token");
            builder.UseSetting("WhatsApp:AllowedCountries:0", "UY"); builder.UseSetting("WhatsApp:ProcessInboundInBackground", "false");
            builder.ConfigureTestServices(services => { services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(_clock); services.RemoveAll<IHostedService>(); });
        });
        using var client = reduced.CreateClient(new() { HandleCookies = false });
        using var preview = await PostAsync(client, "preview", new { token }); Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        await TransactionAsync(app.Services, async (provider, ct) =>
        {
            await provider.GetRequiredService<ILoginMethodRepository>().LockUserAsync(user, ct);
            var account = await provider.GetRequiredService<ApplicationDbContext>().Users.SingleAsync(value => value.Id == user, ct);
            account.LockoutEnabled = true; account.LockoutEnd = _clock.GetUtcNow().AddMinutes(5);
        });
        using var locked = await PostAsync(client, "redeem", new { token, access = "consumer" });
        Assert.Equal(HttpStatusCode.TooManyRequests, locked.StatusCode); Assert.False(locked.Headers.Contains("Set-Cookie"));
        Assert.Equal("Identity.Account.LockedOut", (await locked.Content.ReadFromJsonAsync<JsonObject>(Ct))!["code"]!.GetValue<string>());
    }
}
