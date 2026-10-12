using System.Text.Json;
using System.Net;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantBrowser;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantTurns;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Recorrido;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

public sealed class AssistantBrowserPreparationTests
{
    [Theory]
    [InlineData("consumer")]
    [InlineData("business")]
    [InlineData("platform")]
    public async Task Prepare_without_HTTP_identity_uses_isolated_key_and_restarts_only_the_actor_scenario(string access)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = await AssistantTurnTestSession.CreateAsync(access, acceptNotice: true, workerEnabled: false);
        var directory = Directory.CreateTempSubdirectory("mt-e2e-assistant-").FullName;
        var ready = Path.Combine(directory, "mt-e2e-assistant.ready");
        try
        {
            using (var identity = session.Host.Services.CreateScope())
                Assert.Null(identity.ServiceProvider.GetRequiredService<ICurrentUser>().UserId);
            var bridge = new AssistantFileBridge(session.Host.Services, ready, isolated: true);
            var args = access == "platform"
                ? JsonSerializer.SerializeToElement(new { email = session.Account.Email.Value, access })
                : JsonSerializer.SerializeToElement(new { email = session.Account.Email.Value, access, tenantId = session.TenantId });
            await CommandAsync("prepare", args);
            await AssertPreparedAsync();
            await CommandAsync("quotas", JsonSerializer.SerializeToElement(new
            {
                email = session.Account.Email.Value, access, tenantId = access == "platform" ? null : session.TenantId,
                remainingMessages = 0, monthlyFraction = 1m, notify = access == "business",
            }));
            await CommandAsync("prepare", args);
            await AssertPreparedAsync();
            Assert.Equal(0, session.Provider.Calls);

            async Task CommandAsync(string kind, JsonElement arguments)
            {
                var command = Path.ChangeExtension(ready, ".assistant.json");
                // Platform has no tenant binding; omit the field rather than inventing a scope.
                if (access == "platform" && arguments.TryGetProperty("tenantId", out _))
                    arguments = JsonSerializer.SerializeToElement(new { email = session.Account.Email.Value, access, remainingMessages = 0, monthlyFraction = 1m });
                var id = Guid.NewGuid();
                await File.WriteAllTextAsync(command, JsonSerializer.Serialize(new { id, kind, args = arguments }), ct);
                Assert.True(await bridge.TryExecuteAsync(ct));
                using var response = JsonDocument.Parse(await File.ReadAllTextAsync(command + ".done", ct));
                Assert.Equal(id, response.RootElement.GetProperty("id").GetGuid());
                Assert.True(response.RootElement.GetProperty("ok").GetBoolean(), "The isolated assistant command must succeed.");
                File.Delete(command); File.Delete(command + ".done");
            }
            async Task AssertPreparedAsync()
            {
                using var state = await session.Client.GetAsync(session.Path, ct);
                Assert.Equal(HttpStatusCode.OK, state.StatusCode);
                using var body = JsonDocument.Parse(await state.Content.ReadAsStringAsync(ct));
                Assert.True(body.RootElement.GetProperty("configured").GetBoolean());
                Assert.False(body.RootElement.GetProperty("noticeAccepted").GetBoolean());
                using var scope = session.Host.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Assert.False((await db.Set<AssistantPlatformSettings>().AsNoTracking().SingleAsync(ct)).HasKeyOverride);
                Assert.False(await db.Set<AssistantNoticeAcceptance>().AnyAsync(row => row.OwnerUserId == session.Account.UserId, ct));
                Assert.Equal(0, (await db.Set<AssistantPlatformKeyDailyQuota>().SingleAsync(row => row.UserId == session.Account.UserId, ct)).ConsumedQueries);
            }
        }
        finally { RecorridoPaths.Validate(ready); Directory.Delete(directory, recursive: true); }
    }
}
