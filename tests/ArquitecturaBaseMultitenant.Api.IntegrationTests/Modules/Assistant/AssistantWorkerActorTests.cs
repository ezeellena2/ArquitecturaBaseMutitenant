using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantTurns;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Features;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantWorkerActorTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("consumer")]
    [InlineData("platform")]
    public async Task Gate_or_use_revocation_after_admission_blocks_new_output_and_still_closes_accounting(string access)
    {
        await using var session = await AssistantTurnTestSession.CreateAsync(access);
        var conversation = await session.ConversationAsync();
        using var request = AssistantTurnTestSession.Request(HttpMethod.Post,
            session.Path + $"/conversations/{conversation.Id:D}/messages", AssistantTurnTestSession.Body(conversation), Guid.NewGuid());
        using var response = await session.OpenStreamAsync(request, Ct);
        using var stream = await AssistantSseTestReader.OpenAsync(response, Ct);
        Assert.Equal("start", (await stream.NextAsync(Ct)).Event);
        Assert.Equal("delta", (await stream.NextAsync(Ct)).Event);
        if (access == "consumer") await AssistantTestData.PersonalAsync(session.Factory, false);
        else await PlatformOperators.GrantAsync(session.Host.Services, session.Account.UserId,
            PlatformOperators.All.Where(permission => permission != Domain.Authorization.PlatformPermissions.Assistant.Use).ToArray(), Ct);
        session.Provider.Release();

        var terminal = await stream.TerminalAsync(Ct);
        Assert.Equal("error", terminal.Event);
        Assert.Equal("Failed", terminal.Data.GetProperty("status").GetString());
        Assert.False(string.IsNullOrWhiteSpace(terminal.Data.GetProperty("code").GetString()));
        Assert.DoesNotContain(AssistantTurnTestProvider.Suffix, terminal.Data.GetRawText(), StringComparison.Ordinal);
        using var history = await session.Client.GetAsync(session.Path + $"/conversations/{conversation.Id:D}", Ct);
        Assert.Equal(access == "consumer" ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden, history.StatusCode);
        // El lector puede detectar revocación antes del worker; esperar sólo el cierre técnico, sin leer chat.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        while (true)
        {
            using var verification = session.Host.Services.CreateScope();
            var db = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var operation = await db.Set<AssistantFinancialOperation>().AsNoTracking()
                .SingleAsync(row => row.UserId == session.Account.UserId, deadline.Token);
            if (operation.State == AssistantFinancialState.Completed)
            {
                var quota = await db.Set<AssistantPlatformKeyDailyQuota>().AsNoTracking()
                    .SingleAsync(row => row.UserId == session.Account.UserId, deadline.Token);
                Assert.Equal(1, quota.ConsumedQueries);
                Assert.Equal(0, quota.ReservedQueries);
                break;
            }
            await Task.Delay(25, deadline.Token);
        }
        if (access == "consumer") await AssistantTestData.PersonalAsync(session.Factory, true);
        else await PlatformOperators.GrantAsync(session.Host.Services, session.Account.UserId, PlatformOperators.All, Ct);
        var reopened = await session.HistoryAsync(conversation.Id, Ct);
        Assert.NotNull(reopened);
        Assert.Null(reopened.ActiveTurnId);
        Assert.Contains(reopened.Messages.Items, row => row.Status == Application.Modules.Assistant.Models.AssistantMessageStatus.Failed
            && row.Text == AssistantTurnTestProvider.Prefix);
        Assert.DoesNotContain(reopened.Messages.Items, row => row.Status == Application.Modules.Assistant.Models.AssistantMessageStatus.Generating);
        Assert.Equal(1, session.Provider.Calls);
    }

    [Theory]
    [InlineData("consumer", "my_account")]
    [InlineData("consumer", "my_organizations")]
    [InlineData("business", "org_pending")]
    public async Task Tool_cycle_reads_real_services_under_the_worker_actor_and_consumes_one_query(string access, string toolKey)
    {
        await using var session = await AssistantTurnTestSession.CreateAsync(access);
        session.Provider.ToolKey = toolKey;
        Guid? firstOrganization = null;
        Guid? secondOrganization = null;
        Guid? foreignOrganization = null;
        if (toolKey == "my_organizations")
        {
            firstOrganization = await OrganizationJourney.StageAsync(session.Factory, session.Account, "Worker first own", Ct);
            secondOrganization = await OrganizationJourney.StageAsync(session.Factory, session.Account, "Worker second own", Ct);
            using var other = AssistantTurnTestSession.NewClient(session.Host);
            var outsider = await AccountJourney.RegisterAsync(session.Host, other, Ct);
            foreignOrganization = await OrganizationJourney.StageAsync(session.Factory, outsider, "Worker foreign", Ct);
        }
        else if (toolKey == "org_pending")
            await OrganizationJourney.InviteExpiredAsync(session.Factory, session.Client, session.TenantId!.Value,
                "worker-pending-" + Guid.NewGuid().ToString("N") + "@example.test", Ct);

        var conversation = await session.ConversationAsync();
        session.Provider.Release();
        using var request = AssistantTurnTestSession.Request(HttpMethod.Post,
            session.Path + $"/conversations/{conversation.Id:D}/messages", AssistantTurnTestSession.Body(conversation), Guid.NewGuid());
        using var response = await session.OpenStreamAsync(request, Ct);
        using var stream = await AssistantSseTestReader.OpenAsync(response, Ct);
        Assert.Equal("start", (await stream.NextAsync(Ct)).Event);
        var states = new List<string>();
        while (true)
        {
            var frame = await stream.NextAsync(Ct);
            if (frame.Event == "tool")
            {
                Assert.Equal(toolKey, frame.Data.GetProperty("toolKey").GetString());
                states.Add(frame.Data.GetProperty("state").GetString()!);
                Assert.False(string.IsNullOrWhiteSpace(frame.Data.GetProperty("labelKey").GetString()));
                Assert.Equal(new[] { "labelKey", "state", "toolKey", "turnId" },
                    frame.Data.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
            }
            if (frame.Event is "done" or "error")
            {
                Assert.Equal("done", frame.Event);
                Assert.Equal("Complete", frame.Data.GetProperty("message").GetProperty("status").GetString());
                break;
            }
        }
        Assert.Equal(new[] { "running", "complete" }, states);
        Assert.Equal(2, session.Provider.Calls);
        var data = Assert.Single(session.Provider.ToolResults);
        if (toolKey == "my_account")
        {
            Assert.Contains("loginMethods", data, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(session.Account.Email.Value, data, StringComparison.Ordinal);
        }
        else if (toolKey == "my_organizations")
        {
            Assert.Contains(firstOrganization!.Value.ToString("D"), data, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(secondOrganization!.Value.ToString("D"), data, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(foreignOrganization!.Value.ToString("D"), data, StringComparison.OrdinalIgnoreCase);
        }
        else Assert.Contains("users", data, StringComparison.Ordinal);
        Assert.All(session.Provider.Invocations, invocation =>
        {
            Assert.Equal(session.Account.UserId, invocation.Owner);
            Assert.Equal(access == "consumer" ? Access.Consumer : Access.Business, invocation.Access);
            Assert.Equal(session.TenantId, invocation.TenantId);
            Assert.False(invocation.HasHttpRequest);
            Assert.False(invocation.HasTransaction);
        });
        using var verification = session.Host.Services.CreateScope();
        var db = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var quota = await db.Set<AssistantPlatformKeyDailyQuota>().SingleAsync(row => row.UserId == session.Account.UserId, Ct);
        Assert.Equal(1, quota.ConsumedQueries);
        Assert.Equal(0, quota.ReservedQueries);
    }

    [Fact]
    public async Task Org_pending_includes_whatsapp_number_without_payment_method_without_failing_the_turn()
    {
        await using var session = await AssistantTurnTestSession.CreateAsync("business");
        var displayNumber = PhoneNumber.Create("+5491155551234").Value;
        using (var setup = session.Host.Services.CreateScope())
        {
            var services = setup.ServiceProvider;
            var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
            var saved = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                services.GetRequiredService<ITenantFeatureRepository>().Add(
                    TenantFeature.Create(session.TenantId!.Value, WhatsAppFeature.Key, true, TenantFeatureSource.Manual, null).Value);
                var channel = WhatsAppChannel.Connect(session.TenantId.Value, null, "wa-pending-" + Guid.NewGuid().ToString("N"),
                    "waba-pending-test", displayNumber, "Organización prueba", false, hasPayment: false,
                    WhatsAppChannelSource.Signup, AssistantTurnTestSession.TestTimeZoneId, nowUtc);
                channel.ProtectSecrets([1, 2, 3, 4], null);
                services.GetRequiredService<ApplicationDbContext>().Add(channel);
                services.GetRequiredService<ISecurityEventRepository>().Add(SecurityEvent.ForTenantFeatures(
                    AuditActorKind.System, Guid.Empty, session.TenantId.Value,
                    "Assistant org_pending WhatsApp payment-missing fixture", nowUtc));
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
            Assert.True(saved.IsSuccess, saved.Error.Code);
        }

        session.Provider.ToolKey = "org_pending";
        var conversation = await session.ConversationAsync();
        session.Provider.Release();
        using var request = AssistantTurnTestSession.Request(HttpMethod.Post,
            session.Path + $"/conversations/{conversation.Id:D}/messages", AssistantTurnTestSession.Body(conversation), Guid.NewGuid());
        using var response = await session.OpenStreamAsync(request, Ct);
        using var stream = await AssistantSseTestReader.OpenAsync(response, Ct);
        Assert.Equal("start", (await stream.NextAsync(Ct)).Event);
        var terminal = await stream.TerminalAsync(Ct);
        Assert.Equal("done", terminal.Event);
        Assert.Equal("Complete", terminal.Data.GetProperty("message").GetProperty("status").GetString());
        Assert.Equal(2, session.Provider.Calls);
        var data = Assert.Single(session.Provider.ToolResults);
        using var parsed = JsonDocument.Parse(data);
        var item = Assert.Single(parsed.RootElement.GetProperty("pending").EnumerateArray(),
            entry => entry.GetProperty("area").GetString() == "whatsapp");
        Assert.Equal("numberPaymentMissing", item.GetProperty("kind").GetString());
        Assert.Equal(displayNumber.Value, item.GetProperty("subject").GetString());
    }

    [Fact]
    public async Task Repeated_calls_to_the_same_tool_in_one_round_complete_even_above_distinct_tool_count()
    {
        await using var session = await AssistantTurnTestSession.CreateAsync("platform");
        // Con sólo platform.assistant.use el turno tiene 1 herramienta (help_article); 2 llamadas en la misma vuelta prueban KAN-96.
        await PlatformOperators.GrantAsync(session.Host.Services, session.Account.UserId,
            [PlatformPermissions.Assistant.Use], Ct);
        session.Provider.ToolKey = "help_article";
        session.Provider.ToolCallsPerRound = 2;
        var conversation = await session.ConversationAsync();
        session.Provider.Release();
        using var request = AssistantTurnTestSession.Request(HttpMethod.Post,
            session.Path + $"/conversations/{conversation.Id:D}/messages", AssistantTurnTestSession.Body(conversation), Guid.NewGuid());
        using var response = await session.OpenStreamAsync(request, Ct);
        using var stream = await AssistantSseTestReader.OpenAsync(response, Ct);
        Assert.Equal("start", (await stream.NextAsync(Ct)).Event);
        var states = new List<string>();
        while (true)
        {
            var frame = await stream.NextAsync(Ct);
            if (frame.Event == "tool")
            {
                Assert.Equal("help_article", frame.Data.GetProperty("toolKey").GetString());
                states.Add(frame.Data.GetProperty("state").GetString()!);
            }
            if (frame.Event is "done" or "error")
            {
                Assert.Equal("done", frame.Event);
                Assert.Equal("Complete", frame.Data.GetProperty("message").GetProperty("status").GetString());
                break;
            }
        }
        Assert.Equal(new[] { "running", "complete", "running", "complete" }, states);
        Assert.Equal(2, session.Provider.Calls);
        Assert.Equal(2, session.Provider.ToolResults.Count);
    }
}
