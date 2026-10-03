using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Platform;

[Collection(ApiTestGroup.Name)]
public sealed class PlatformSettingsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Owner_updates_global_settings_and_a_stale_version_rolls_back_its_event()
    {
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        using var client = isolated.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(isolated, client, Ct);
        await GrantAsync(isolated, account.UserId, PlatformRole.Owner);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);

        using var initial = await client.GetAsync("/api/platform/settings", Ct);
        Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
        using var initialJson = JsonDocument.Parse(await initial.Content.ReadAsStringAsync(Ct));
        var originalVersion = initialJson.RootElement.GetProperty("version").GetUInt32();
        using var saved = await client.PutAsJsonAsync("/api/platform/settings", new
        {
            consumerSignup = "Closed", businessSignup = "RequiresApproval", maxOwnedOrganizations = 3,
            version = originalVersion, reason = "Control del alta de prueba",
        }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);
        using var updated = await client.GetAsync("/api/platform/settings", Ct);
        using var updatedJson = JsonDocument.Parse(await updated.Content.ReadAsStringAsync(Ct));
        Assert.Equal("Closed", updatedJson.RootElement.GetProperty("consumerSignup").GetString());
        Assert.Equal("RequiresApproval", updatedJson.RootElement.GetProperty("businessSignup").GetString());
        Assert.Equal(3, updatedJson.RootElement.GetProperty("maxOwnedOrganizations").GetInt32());
        Assert.NotEqual(originalVersion, updatedJson.RootElement.GetProperty("version").GetUInt32());

        using var stale = await client.PutAsJsonAsync("/api/platform/settings", new
        {
            consumerSignup = "Open", businessSignup = "Open", maxOwnedOrganizations = 1,
            version = originalVersion, reason = "Cambio obsoleto",
        }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var staleJson = JsonDocument.Parse(await stale.Content.ReadAsStringAsync(Ct));
        Assert.Equal("General.ConcurrencyConflict", staleJson.RootElement.GetProperty("code").GetString());
        await using var scope = isolated.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await context.SecurityEvents.CountAsync(item => item.ActorId == account.UserId
            && item.Type == Domain.Auditing.SecurityEventType.PlatformSettingsChanged, Ct));
        var ev = await context.SecurityEvents.AsNoTracking().SingleAsync(item => item.ActorId == account.UserId
            && item.Type == Domain.Auditing.SecurityEventType.PlatformSettingsChanged, Ct);
        using var diff = JsonDocument.Parse(ev.Changes);
        Assert.Equal("Open", diff.RootElement.GetProperty("consumerSignup").GetProperty("before").GetString());
        Assert.Equal("Closed", diff.RootElement.GetProperty("consumerSignup").GetProperty("after").GetString());
        Assert.Equal(3, diff.RootElement.GetProperty("maxOwnedOrganizations").GetProperty("after").GetInt32());
        Assert.DoesNotContain("Control del alta de prueba", ev.Changes);
        Assert.Null(ev.TargetTenantId);
        Assert.Null(ev.TargetAccountId);
        Assert.Equal(Domain.Settings.ConsumerSignupMode.Closed, (await context.PlatformSettings.AsNoTracking().SingleAsync(Ct)).ConsumerSignup);
    }

    [Fact]
    public async Task Settings_reject_anonymous_consumer_and_support_access()
    {
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        using var client = isolated.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var anonymous = await client.GetAsync("/api/platform/settings", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        var account = await AccountJourney.RegisterAsync(isolated, client, Ct);
        using var consumer = await client.GetAsync("/api/platform/settings", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, consumer.StatusCode);
        await GrantAsync(isolated, account.UserId, PlatformRole.Support);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        using var support = await client.GetAsync("/api/platform/settings", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, support.StatusCode);
        using var supportUpdate = await client.PutAsJsonAsync("/api/platform/settings", new
        {
            consumerSignup = "Open", businessSignup = "Open", maxOwnedOrganizations = 2, version = 1u, reason = "Cambio no autorizado",
        }, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, supportUpdate.StatusCode);
    }

    [Fact]
    public async Task Invalid_settings_or_missing_reason_do_not_write_an_event()
    {
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        using var client = isolated.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(isolated, client, Ct);
        await GrantAsync(isolated, account.UserId, PlatformRole.Owner);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        using var invalid = await client.PutAsJsonAsync("/api/platform/settings", new
        {
            consumerSignup = "Open", businessSignup = "Open", maxOwnedOrganizations = 0, version = 1u, reason = " ",
        }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var problem = JsonDocument.Parse(await invalid.Content.ReadAsStringAsync(Ct));
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("reason", out _));
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("maxOwnedOrganizations", out _));
        await using var scope = isolated.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().SecurityEvents
            .AnyAsync(item => item.ActorId == account.UserId && item.Type == Domain.Auditing.SecurityEventType.PlatformSettingsChanged, Ct));
    }

    private static async Task GrantAsync(ApiFactory factory, Guid userId, PlatformRole role)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var manager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await manager.FindByIdAsync(userId.ToString("D"));
            Assert.NotNull(user);
            user.GrantPlatformOperator();
            Assert.True((await manager.UpdateAsync(user)).Succeeded);
            services.GetRequiredService<IPlatformRoleAssignmentRepository>().Add(PlatformRoleAssignment.Create(userId, role));
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
    }
}
