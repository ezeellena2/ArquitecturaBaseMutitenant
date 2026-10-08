using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

public sealed class WhatsAppOwnerSeedTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Trusted_owner_phone_is_verified_once_without_personal_profile_or_primary_change()
    {
        await using var host = factory.WithWebHostBuilder(builder => builder.UseSetting("Seed:PlatformOwner:Email", "phone-operator@example.test")
            .UseSetting("Seed:PlatformOwner:Phone", "+5491134567890"));
        using var client = host.CreateClient();
        await host.Services.SeedDatabaseAsync(Ct);
        await host.Services.SeedDatabaseAsync(Ct);
        await using var scope = host.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await context.Users.AsNoTracking().SingleAsync(value => value.Email == "phone-operator@example.test", Ct);
        Assert.True(user.IsPlatformOperator);
        Assert.Empty(await context.UserTenantAccesses.Where(value => value.UserId == user.Id).ToArrayAsync(Ct));
        var methods = await context.LoginMethods.AsNoTracking().Where(value => value.UserId == user.Id).ToArrayAsync(Ct);
        Assert.Equal(2, methods.Length);
        var phone = Assert.Single(methods, value => value.Type == ArquitecturaBaseMultitenant.Domain.Authentication.LoginMethodType.Phone);
        Assert.NotNull(phone.VerifiedAtUtc);
        Assert.False(phone.IsPrimary);
        Assert.True(Assert.Single(methods, value => value.Type == ArquitecturaBaseMultitenant.Domain.Authentication.LoginMethodType.Email).IsPrimary);
    }

    [Fact]
    public async Task Trusted_phone_never_converts_an_ordinary_account_into_operator()
    {
        using var browser = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var person = await AccountJourney.RegisterAsync(factory, browser, Ct);
        await using var host = factory.WithWebHostBuilder(builder => builder.UseSetting("Seed:PlatformOwner:Email", person.Email.Value)
            .UseSetting("Seed:PlatformOwner:Phone", "+5491145678901"));
        using var client = host.CreateClient();
        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Services.SeedDatabaseAsync(Ct));
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False((await context.Users.AsNoTracking().SingleAsync(value => value.Id == person.UserId, Ct)).IsPlatformOperator);
        Assert.False(await context.LoginMethods.AnyAsync(value => value.UserId == person.UserId && value.Value == "+5491145678901", Ct));
    }
}
