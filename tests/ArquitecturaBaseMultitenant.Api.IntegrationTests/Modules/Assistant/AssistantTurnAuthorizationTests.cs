using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantTurns;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantTurnAuthorizationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("consumer")]
    [InlineData("business")]
    [InlineData("platform")]
    public async Task Full_permission_owner_or_operator_cannot_admit_or_stop_another_owners_turn(string access)
    {
        await using var session = await AssistantTurnTestSession.CreateAsync(access);
        using var other = AssistantTurnTestSession.NewClient(session.Host);
        var outsider = access == "platform" ? await AssistantTurnTestSession.PlatformAccountAsync(session.Host, other)
            : await AccountJourney.RegisterAsync(session.Host, other, Ct);
        Guid conversationId;
        uint version;
        if (access == "platform")
        {
            using var scope = session.Host.Services.CreateScope();
            var services = scope.ServiceProvider;
            var conversation = PlatformAssistantConversation.Create(outsider.UserId,
                services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
            var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                services.GetRequiredService<ApplicationDbContext>().Add(conversation);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
            Assert.True(result.IsSuccess, result.Error.Code);
            conversationId = conversation.Id;
            version = conversation.Version;
        }
        else
        {
            var tenantId = access == "business" ? session.TenantId!.Value : await AssistantTestData.ActiveTenantAsync(other);
            if (access == "business") await OrganizationJourney.AddActiveMemberAsync(session.Factory, tenantId, outsider.UserId, Ct);
            var conversation = await AssistantTestData.ConversationAsync(session.Factory, tenantId, outsider.UserId,
                access == "business" ? Access.Business : Access.Consumer);
            conversationId = conversation.Id;
            version = conversation.Version;
        }
        var body = JsonSerializer.Serialize(new { text = "No debe entrar al proveedor", attachmentIds = Array.Empty<Guid>(), conversationVersion = version });
        using (var send = AssistantTurnTestSession.Request(HttpMethod.Post,
            session.Path + $"/conversations/{conversationId:D}/messages", body, Guid.NewGuid()))
        using (var denied = await session.Client.SendAsync(send, Ct))
            await AssistantTurnTestSession.AssertProblemAsync(denied, HttpStatusCode.NotFound, AssistantErrors.ConversationNotFoundCode);
        using (var stop = AssistantTurnTestSession.Request(HttpMethod.Post,
            session.Path + $"/conversations/{conversationId:D}/turns/{Guid.NewGuid():D}/stop", "{}", Guid.NewGuid()))
        using (var denied = await session.Client.SendAsync(stop, Ct))
            await AssistantTurnTestSession.AssertProblemAsync(denied, HttpStatusCode.NotFound);
        Assert.Equal(0, session.Provider.Calls);
        Assert.Equal(0, session.Provider.TitleCalls);
        Assert.Empty(session.Provider.Invocations);
    }

    [Fact]
    public async Task Invalid_input_stale_version_and_foreign_attachment_fail_before_stream_or_financial_admission()
    {
        await using var session = await AssistantTurnTestSession.CreateAsync();
        var conversation = await session.ConversationAsync();
        var path = session.Path + $"/conversations/{conversation.Id:D}/messages";
        using (var empty = AssistantTurnTestSession.Request(HttpMethod.Post, path,
            AssistantTurnTestSession.Body(conversation, ""), Guid.NewGuid()))
        using (var rejected = await session.Client.SendAsync(empty, Ct))
            await AssistantTurnTestSession.AssertProblemAsync(rejected, HttpStatusCode.BadRequest, "Validation.Failed");

        using (var changed = await session.Client.PutAsJsonAsync(session.Path + $"/conversations/{conversation.Id:D}",
            new { title = "Título manual antes del envío", isSaved = true, version = conversation.Version }, Ct))
            Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        using (var stale = AssistantTurnTestSession.Request(HttpMethod.Post, path,
            AssistantTurnTestSession.Body(conversation), Guid.NewGuid()))
        using (var rejected = await session.Client.SendAsync(stale, Ct))
            await AssistantTurnTestSession.AssertProblemAsync(rejected, HttpStatusCode.Conflict, "General.ConcurrencyConflict");

        var current = await session.HistoryAsync(conversation.Id, Ct);
        Assert.NotNull(current);
        using var other = AssistantTurnTestSession.NewClient(session.Host);
        var outsider = await AccountJourney.RegisterAsync(session.Host, other, Ct);
        var foreignTenant = await AssistantTestData.ActiveTenantAsync(other);
        var foreignConversation = await AssistantTestData.ConversationAsync(session.Factory, foreignTenant, outsider.UserId, Access.Consumer);
        AssistantAttachment foreignFile;
        using (var setup = session.Host.Services.CreateScope())
        {
            var services = setup.ServiceProvider;
            using var tenant = services.GetRequiredService<ITenantScope>().Enter(foreignTenant);
            foreignFile = AssistantAttachment.Create(foreignConversation.Id, "foreign-test.png", "image/png",
                "isolated-foreign-file", 1, services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
            var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                services.GetRequiredService<ApplicationDbContext>().Add(foreignFile);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
            Assert.True(result.IsSuccess, result.Error.Code);
        }
        var attachmentBody = JsonSerializer.Serialize(new
        {
            text = "Adjunto que no pertenece a la conversación", attachmentIds = new[] { foreignFile.Id },
            conversationVersion = current.Conversation.Version,
        });
        using (var attachment = AssistantTurnTestSession.Request(HttpMethod.Post, path, attachmentBody, Guid.NewGuid()))
        using (var rejected = await session.Client.SendAsync(attachment, Ct))
            await AssistantTurnTestSession.AssertProblemAsync(rejected, HttpStatusCode.NotFound, AssistantErrors.AttachmentNotFoundCode);
        Assert.Equal(0, session.Provider.Calls);
        Assert.Equal(0, session.Provider.TitleCalls);
        var untouched = await session.HistoryAsync(conversation.Id, Ct);
        Assert.NotNull(untouched);
        Assert.Empty(untouched.Messages.Items);
        Assert.Null(untouched.ActiveTurnId);
        Assert.Equal(current.Conversation.Version, untouched.Conversation.Version);
        using var scope = session.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.Set<AssistantFinancialOperation>().AnyAsync(row => row.UserId == session.Account.UserId, Ct));
        Assert.False(await db.Set<AssistantPlatformKeyDailyQuota>().AnyAsync(row => row.UserId == session.Account.UserId, Ct));
    }

    [Fact]
    public async Task Stopping_an_unknown_turn_of_an_owned_conversation_returns_not_found_without_admitting_work()
    {
        await using var session = await AssistantTurnTestSession.CreateAsync();
        var conversation = await session.ConversationAsync();
        using var stop = AssistantTurnTestSession.Request(HttpMethod.Post,
            session.Path + $"/conversations/{conversation.Id:D}/turns/{Guid.NewGuid():D}/stop", "{}", Guid.NewGuid());
        using var response = await session.Client.SendAsync(stop, Ct);
        await AssistantTurnTestSession.AssertProblemAsync(response, HttpStatusCode.NotFound, AssistantErrors.TurnNotFoundCode);
        Assert.Equal(0, session.Provider.Calls);
        var history = await session.HistoryAsync(conversation.Id, Ct);
        Assert.NotNull(history);
        Assert.Empty(history.Messages.Items);
        Assert.Null(history.ActiveTurnId);
    }

    [Fact]
    public async Task Exhausted_daily_quota_rejects_new_turns_but_fixed_queries_still_use_real_tools_without_the_provider()
    {
        await using var session = await AssistantTurnTestSession.CreateAsync();
        var conversation = await session.ConversationAsync();
        using (var scope = session.Host.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var day = DateOnly.FromDateTime(services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
            var quota = AssistantPlatformKeyDailyQuota.Create(session.Account.UserId, day);
            for (var index = 0; index < 50; index++) { quota.Reserve(); quota.Consume(); }
            var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                services.GetRequiredService<ApplicationDbContext>().Add(quota);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
            Assert.True(result.IsSuccess, result.Error.Code);
        }
        using (var send = AssistantTurnTestSession.Request(HttpMethod.Post,
            session.Path + $"/conversations/{conversation.Id:D}/messages", AssistantTurnTestSession.Body(conversation), Guid.NewGuid()))
        using (var limited = await session.Client.SendAsync(send, Ct))
        {
            await AssistantTurnTestSession.AssertProblemAsync(limited, HttpStatusCode.TooManyRequests, AssistantErrors.LimitDailyReachedCode);
            Assert.NotNull(limited.Headers.RetryAfter);
        }
        using (var quick = AssistantTurnTestSession.Request(HttpMethod.Post, session.Path + "/quick/my_signin", "{}", Guid.NewGuid()))
        using (var free = await session.Client.SendAsync(quick, Ct))
            Assert.Equal(HttpStatusCode.OK, free.StatusCode);
        Assert.Equal(0, session.Provider.Calls);
        Assert.Equal(0, session.Provider.TitleCalls);
        var history = await session.HistoryAsync(conversation.Id, Ct);
        Assert.NotNull(history);
        Assert.Empty(history.Messages.Items);
        Assert.Null(history.ActiveTurnId);
        using var verification = session.Host.Services.CreateScope();
        var db = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var unchanged = await db.Set<AssistantPlatformKeyDailyQuota>().SingleAsync(row => row.UserId == session.Account.UserId, Ct);
        Assert.Equal(50, unchanged.ConsumedQueries);
        Assert.Equal(0, unchanged.ReservedQueries);
        Assert.False(await db.Set<AssistantFinancialOperation>().AnyAsync(row => row.UserId == session.Account.UserId, Ct));
    }
}
