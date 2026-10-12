using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantTurns;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Retention;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantTurnContextPrivacyTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public async Task Historical_subject_privacy_survives_binding_purge_and_is_checked_before_admission(
        bool purgeBinding, bool deletionStarted)
    {
        await using var session = await AssistantTurnTestSession.CreateAsync("business");
        const string name = "Persona histórica del fixture";
        var memberId = await OrganizationJourney.AddNamedMemberAsync(session.Factory, session.TenantId!.Value, name, Ct);
        Guid subject;
        using (var identityScope = session.Host.Services.CreateScope())
        {
            var services = identityScope.ServiceProvider;
            using var tenant = services.GetRequiredService<ITenantScope>().Enter(session.TenantId.Value);
            subject = (await services.GetRequiredService<ApplicationDbContext>().Set<Member>().AsNoTracking()
                .SingleAsync(row => row.Id == memberId, Ct)).UserId
                ?? throw new InvalidOperationException("The fixture member must have a linked account.");
        }
        var quickKey = Guid.NewGuid();
        using var quick = AssistantTurnTestSession.Request(HttpMethod.Post, session.Path + "/quick/active_users", "{}", quickKey);
        using var quickResponse = await session.Client.SendAsync(quick, Ct);
        Assert.Equal(HttpStatusCode.OK, quickResponse.StatusCode);
        using var quickJson = JsonDocument.Parse(await quickResponse.Content.ReadAsStringAsync(Ct));
        Assert.Contains(quickJson.RootElement.GetProperty("result").GetProperty("data").GetProperty("people").EnumerateArray(),
            row => row.GetProperty("displayName").GetString() == name);
        var conversation = quickJson.RootElement.GetProperty("conversation")
            .Deserialize<AssistantConversationSummaryResponse>(AssistantTurnTestSession.Json)!;
        Guid bindingId;
        using (var setup = session.Host.Services.CreateScope())
        {
            var services = setup.ServiceProvider;
            using var tenant = services.GetRequiredService<ITenantScope>().Enter(session.TenantId.Value);
            var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var db = services.GetRequiredService<ApplicationDbContext>();
                var message = await db.Set<AssistantMessage>().SingleAsync(row => row.ConversationId == conversation.Id && row.Role == "Assistant", ct);
                // Una respuesta histórica puede repetir el nombre autorizado: conservar su provenance real de Quick.
                message.Checkpoint(message.Text + " " + name);
                var binding = await db.Set<AssistantDurableOperation>().SingleAsync(row => row.OwnerUserId == session.Account.UserId && row.Key == quickKey, ct);
                Assert.Contains(subject, binding.SubjectUserIds);
                if (purgeBinding)
                {
                    var now = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
                    db.Entry(binding).Property(row => row.CreatedAtUtc).CurrentValue = now.AddHours(-25);
                    var index = await db.Set<AssistantSnapshotRetentionIndex>().SingleAsync(row => row.OperationId == binding.Id, ct);
                    db.Entry(index).Property(row => row.ExpiresAtUtc).CurrentValue = now.AddSeconds(-1);
                }
                return Result.Success(binding.Id);
            }, CommitPolicy.OnSuccess, Ct);
            Assert.True(result.IsSuccess, result.Error.Code);
            bindingId = result.Value;
        }
        if (purgeBinding)
        {
            await ActivatorUtilities.CreateInstance<AssistantSnapshotRetentionWorker>(session.Host.Services).RunOnceAsync(Ct);
            using var read = session.Host.Services.CreateScope();
            var services = read.ServiceProvider;
            using var tenant = services.GetRequiredService<ITenantScope>().Enter(session.TenantId.Value);
            var binding = await services.GetRequiredService<ApplicationDbContext>().Set<AssistantDurableOperation>()
                .AsNoTracking().SingleAsync(row => row.Id == bindingId, Ct);
            Assert.Null(binding.SnapshotJson);
            Assert.Empty(binding.SubjectUserIds);
        }
        if (deletionStarted) await ClaimDeletionAsync(session, subject);
        using var send = AssistantTurnTestSession.Request(HttpMethod.Post,
            session.Path + $"/conversations/{conversation.Id:D}/messages", AssistantTurnTestSession.Body(conversation), Guid.NewGuid());
        using var response = await session.OpenStreamAsync(send, Ct);
        if (deletionStarted)
        {
            await AssistantTurnTestSession.AssertProblemAsync(response, HttpStatusCode.NotFound, AssistantErrors.ConversationNotFoundCode);
            Assert.Equal(0, session.Provider.Calls);
            using var verification = session.Host.Services.CreateScope();
            var db = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(await db.Set<AssistantFinancialOperation>().AnyAsync(row => row.UserId == session.Account.UserId, Ct));
            Assert.False(await db.Set<AssistantPlatformKeyDailyQuota>().AnyAsync(row => row.UserId == session.Account.UserId, Ct));
        }
        else
        {
            using var stream = await AssistantSseTestReader.OpenAsync(response, Ct);
            Assert.Equal("start", (await stream.NextAsync(Ct)).Event);
            Assert.Equal("delta", (await stream.NextAsync(Ct)).Event);
            Assert.Contains(name, Assert.Single(session.Provider.Contexts), StringComparison.Ordinal);
            session.Provider.Release();
            Assert.Equal("done", (await stream.TerminalAsync(Ct)).Event);
            Assert.Equal(1, session.Provider.Calls);
        }
    }

    [Fact]
    public async Task Legacy_history_without_verifiable_subject_metadata_fails_closed_without_a_provider_call()
    {
        await using var session = await AssistantTurnTestSession.CreateAsync();
        var conversation = await session.ConversationAsync();
        using (var setup = session.Host.Services.CreateScope())
        {
            var services = setup.ServiceProvider;
            using var tenant = services.GetRequiredService<ITenantScope>().Enter(session.TenantId!.Value);
            var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                var legacy = AssistantMessage.Create(conversation.Id, "Assistant", "Complete", "Copia histórica sin provenance",
                    services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
                services.GetRequiredService<ApplicationDbContext>().Add(legacy);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
            Assert.True(result.IsSuccess, result.Error.Code);
        }
        using var send = AssistantTurnTestSession.Request(HttpMethod.Post,
            session.Path + $"/conversations/{conversation.Id:D}/messages", AssistantTurnTestSession.Body(conversation), Guid.NewGuid());
        using var response = await session.OpenStreamAsync(send, Ct);
        await AssistantTurnTestSession.AssertProblemAsync(response, HttpStatusCode.NotFound, AssistantErrors.ConversationNotFoundCode);
        Assert.Equal(0, session.Provider.Calls);
        using var verification = session.Host.Services.CreateScope();
        Assert.False(await verification.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<AssistantFinancialOperation>()
            .AnyAsync(row => row.UserId == session.Account.UserId, Ct));
    }

    private static async Task ClaimDeletionAsync(AssistantTurnTestSession session, Guid subject)
    {
        using var scope = session.Host.Services.CreateScope();
        var services = scope.ServiceProvider;
        var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var now = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
            var requested = await services.GetRequiredService<IUserRepository>().RequestDeletionAsync(subject, "Fixture de privacidad", now, 30, ct);
            if (requested.IsFailure) return requested;
            var db = services.GetRequiredService<ApplicationDbContext>();
            var identity = await db.Users.SingleAsync(row => row.Id == subject, ct);
            db.Entry(identity).Property(row => row.DeletionScheduledForUtc).CurrentValue = now.AddDays(-1);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(result.IsSuccess, result.Error.Code);
        var claim = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var now = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
            var id = await services.GetRequiredService<IAccountDeletionRepository>().ClaimAsync(Guid.NewGuid(), now, now.AddMinutes(15), ct);
            Assert.Equal(subject, id);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(claim.IsSuccess, claim.Error.Code);
    }
}
