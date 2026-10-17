using System.Net.Http.Headers;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

/// <summary>
/// La parte del Asistente en la matriz (E9): en los tres lados, la conversación, el mensaje, el adjunto y el turno de otra
/// organización, persona u operador responden 404, también dentro de una conversación propia.
/// </summary>
public sealed partial class RouteIsolationMatrixTests
{
    private static readonly (string Side, Caller Caller, string Quick)[] AssistantSides =
    [
        ("me", Caller.Consumer, "my_signin"),
        ("organization", Caller.Business, "active_users"),
        ("platform", Caller.Platform, "pending_organizations"),
    ];

    static partial void AddAssistantCases(Dictionary<string, RouteCase> cases)
    {
        foreach (var (side, caller, quick) in AssistantSides)
        {
            var root = $"/api/{side}/assistant";
            string Kind(string kind) => $"assistant.{side}.{kind}";
            Probe As(string path, object? body = null) => new(caller, path, body);
            string ForeignPath(World w) => $"{root}/conversations/{w.B(Kind("conversation"))}";
            string OwnPath(World w) => $"{root}/conversations/{w.A(Kind("conversation"))}";

            cases[$"GET {root}/conversations/{{id:guid}}"] = Foreign(w => As(ForeignPath(w)));
            cases[$"PUT {root}/conversations/{{id:guid}}"] = Foreign(w => As(ForeignPath(w),
                new { title = "Matriz", isSaved = true, version = 1 }));
            cases[$"DELETE {root}/conversations/{{id:guid}}"] = Foreign(w => As(ForeignPath(w) + "?version=1"));
            cases[$"GET {root}/conversations/{{id:guid}}/export"] = Foreign(w => As(ForeignPath(w) + "/export?format=markdown"));
            cases[$"POST {root}/conversations/{{id:guid}}/attachments"] = Foreign(w => As(ForeignPath(w) + "/attachments", Upload()));
            cases[$"POST {root}/conversations/{{id:guid}}/messages"] = Foreign(w => As(ForeignPath(w) + "/messages",
                new { text = "Hola", attachmentIds = Array.Empty<Guid>(), conversationVersion = 1 }));
            cases[$"GET {root}/conversations/{{id:guid}}/attachments/{{attachmentid:guid}}"] = Foreign(
                w => As($"{ForeignPath(w)}/attachments/{w.B(Kind("attachment"))}"),
                w => As($"{OwnPath(w)}/attachments/{w.B(Kind("attachment"))}"));
            cases[$"DELETE {root}/conversations/{{id:guid}}/attachments/{{attachmentid:guid}}"] = Foreign(
                w => As($"{ForeignPath(w)}/attachments/{w.B(Kind("attachment"))}?version=1"),
                w => As($"{OwnPath(w)}/attachments/{w.B(Kind("attachment"))}?version=1"));
            cases[$"PUT {root}/conversations/{{id:guid}}/messages/{{messageid:guid}}/feedback"] = Foreign(
                w => As($"{ForeignPath(w)}/messages/{w.B(Kind("message"))}/feedback", new { rating = "up", version = 1 }),
                w => As($"{OwnPath(w)}/messages/{w.B(Kind("message"))}/feedback", new { rating = "up", version = 1 }));
            cases[$"POST {root}/conversations/{{id:guid}}/turns/{{turnid:guid}}/stop"] = Foreign(
                w => As($"{ForeignPath(w)}/turns/{w.B(Kind("turn"))}/stop"),
                w => As($"{OwnPath(w)}/turns/{w.B(Kind("turn"))}/stop"));
            cases[$"POST {root}/quick/{{key}}"] = Foreign(w => As($"{root}/quick/{quick}",
                new { conversationId = w.B(Kind("conversation")), version = 1 }));
        }

        cases["PUT /api/platform/assistant/models/{id:guid}"] = Shared(PlatformWide);
        cases["GET /api/platform/tenants/{tenantid:guid}/assistant/policy"] = Shared(PlatformWide);
        cases["PUT /api/platform/tenants/{tenantid:guid}/assistant/policy"] = Shared(PlatformWide);
    }

    static partial void AddAssistantSeed(List<Func<World, Task>> seeds) => seeds.Add(SeedAssistantAsync);

    private static async Task SeedAssistantAsync(World world)
    {
        var factory = world.Factory;
        await AssistantTestData.EnableAsync(factory, world.OrganizationA);
        await AssistantTestData.EnableAsync(factory, world.OrganizationB);
        using (var scope = factory.Services.CreateScope())
        {
            var personal = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<AssistantPlatformSettings>()
                .AsNoTracking().Select(settings => settings.PersonalEnabled).SingleAsync(Ct);
            world.Cleanups.Add(() => AssistantTestData.PersonalAsync(factory, personal));
        }
        await AssistantTestData.PersonalAsync(factory, true);
        foreach (var (side, caller, _) in AssistantSides)
            await AssistantExecutionActorTests.AcceptNoticeAsync(world.Client(caller), $"/api/{side}/assistant/notice");

        await ConversationAsync(world, "organization", world.OrganizationA, world.OwnerA.UserId, Access.Business, world.Own, null);
        await ConversationAsync(world, "organization", world.OrganizationB, world.OwnerB.UserId, Access.Business, world.Foreign, world.OrganizationMark);
        await ConversationAsync(world, "me", world.PersonalA, world.OwnerA.UserId, Access.Consumer, world.Own, null);
        await ConversationAsync(world, "me", world.PersonalB, world.OwnerB.UserId, Access.Consumer, world.Foreign, world.PersonMark);
        await PlatformConversationAsync(world, world.OperatorA.UserId, world.Own, null);
        await PlatformConversationAsync(world, world.OperatorB.UserId, world.Foreign, world.OperatorMark);
        foreach (var (side, caller, _) in AssistantSides)
            world.OpenDoors.Add((caller, $"/api/{side}/assistant/conversations/{world.A($"assistant.{side}.conversation")}"));
        world.SeededTables.AddRange(["tenant.AssistantConversations", "tenant.AssistantMessages", "tenant.AssistantAttachments",
            "platform.PlatformAssistantConversations", "platform.PlatformAssistantTurns"]);
    }

    /// <summary>Una conversación con su mensaje, su adjunto y un turno terminado; la marca va en el título y el texto.</summary>
    private static async Task ConversationAsync(World world, string side, Guid tenantId, Guid owner, Access access,
        Dictionary<string, Guid> into, string? mark)
    {
        using var scope = world.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var conversation = AssistantConversation.Create(owner, access, nowUtc);
        conversation.Update(mark ?? "Matriz propia", true, nowUtc);
        var message = AssistantMessage.Create(conversation.Id, "User", "Complete", mark ?? "Matriz propia", nowUtc);
        var attachment = AssistantAttachment.Create(conversation.Id, "matriz.txt", "text/plain", Guid.NewGuid().ToString("N"), 1, nowUtc);
        var turn = AssistantTurn.Create(conversation.Id, nowUtc);
        turn.Finish(nameof(AssistantTurnStatus.Complete));
        var saved = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            services.GetRequiredService<ApplicationDbContext>().AddRange(conversation, message, attachment, turn);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(saved.IsSuccess);
        Remember(into, $"assistant.{side}", conversation.Id, message.Id, attachment.Id, turn.Id);
    }

    private static async Task PlatformConversationAsync(World world, Guid owner, Dictionary<string, Guid> into, string? mark)
    {
        using var scope = world.Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var conversation = PlatformAssistantConversation.Create(owner, nowUtc);
        conversation.Update(mark ?? "Matriz propia", true, nowUtc);
        var message = PlatformAssistantMessage.Create(conversation.Id, "User", "Complete", mark ?? "Matriz propia", nowUtc);
        var attachment = PlatformAssistantAttachment.Create(conversation.Id, "matriz.txt", "text/plain", Guid.NewGuid().ToString("N"), 1, nowUtc);
        var turn = PlatformAssistantTurn.Create(conversation.Id, nowUtc);
        turn.Finish(nameof(AssistantTurnStatus.Complete));
        var saved = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            services.GetRequiredService<ApplicationDbContext>().AddRange(conversation, message, attachment, turn);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(saved.IsSuccess);
        Remember(into, "assistant.platform", conversation.Id, message.Id, attachment.Id, turn.Id);
    }

    private static void Remember(Dictionary<string, Guid> into, string prefix, Guid conversation, Guid message, Guid attachment, Guid turn)
    {
        into[prefix + ".conversation"] = conversation;
        into[prefix + ".message"] = message;
        into[prefix + ".attachment"] = attachment;
        into[prefix + ".turn"] = turn;
    }

    private static MultipartFormDataContent Upload()
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent([137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "matriz.png");
        form.Add(new StringContent("1"), "conversationVersion");
        return form;
    }
}
