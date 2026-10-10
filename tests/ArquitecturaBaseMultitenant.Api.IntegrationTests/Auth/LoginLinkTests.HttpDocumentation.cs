using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

public sealed partial class LoginLinkTests
{
    [Theory]
    [InlineData("preview", "es", true)]
    [InlineData("preview", "en", true)]
    [InlineData("redeem", "es", true)]
    [InlineData("redeem", "en", true)]
    [InlineData("preview", "es", false)]
    [InlineData("preview", "en", false)]
    [InlineData("redeem", "es", false)]
    [InlineData("redeem", "en", false)]
    public async Task Account_state_problem_matches_the_documented_example_without_an_invented_delay(string action, string language, bool locked)
    {
        await using var documentation = factory.WithWebHostBuilder(builder => builder.UseDevelopmentTestOwner());
        using var docClient = documentation.CreateClient();
        var doc = await docClient.GetFromJsonAsync<JsonObject>("/openapi/v1.json", Ct);
        await using var app = Host();
        using var setup = app.CreateClient();
        var (user, _, token) = await PrepareAsync(app, setup);
        await TransactionAsync(app.Services, async (provider, ct) =>
        {
            await provider.GetRequiredService<ILoginMethodRepository>().LockUserAsync(user, ct);
            var account = await provider.GetRequiredService<ApplicationDbContext>().Users.SingleAsync(value => value.Id == user, ct);
            if (locked)
            {
                account.LockoutEnabled = true;
                account.LockoutEnd = _clock.GetUtcNow().AddMinutes(5);
            }
            else Assert.True(account.Suspend().IsSuccess);
        });
        using var client = app.CreateClient(new() { HandleCookies = false });
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
        using var response = await PostAsync(client, action, new { token, access = "consumer" });
        var status = locked ? 429 : 403;
        Assert.Equal((HttpStatusCode)status, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.False(response.Headers.Contains("Retry-After"));
        var problem = await response.Content.ReadFromJsonAsync<JsonObject>(Ct);
        var code = locked ? "Identity.Account.LockedOut" : "Identity.Account.Suspended";
        Assert.Equal(code, problem!["code"]!.GetValue<string>());
        Assert.Null(problem["retryAfter"]);
        var example = doc!["paths"]!["/api/auth/login-link/" + action]!["post"]!["responses"]![status.ToString(System.Globalization.CultureInfo.InvariantCulture)]!["content"]!["application/problem+json"]!["examples"]![code + "." + language]!["value"]!;
        Assert.Equal(problem["status"]!.GetValue<int>(), example["status"]!.GetValue<int>());
        Assert.Equal(problem["detail"]!.GetValue<string>(), example["detail"]!.GetValue<string>());
        Assert.Null(example["retryAfter"]);
        Assert.Null(example["errors"]);
    }
}
