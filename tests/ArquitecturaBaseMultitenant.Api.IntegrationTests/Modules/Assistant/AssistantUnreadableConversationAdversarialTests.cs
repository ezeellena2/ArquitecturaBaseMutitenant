using System.Net;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantTurns;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

/// <summary>
/// Revisión adversarial de la B14 (0064 §6): una conversación que esta versión no sabe leer tampoco se exporta ni recibe un
/// turno nuevo. Las dos rutas leen sus mensajes con <c>Enum.Parse</c>: la exportación da 500, y el turno se admite, reserva
/// el cupo y deja al worker con un contexto que no puede armar.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class AssistantUnreadableConversationAdversarialTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_unreadable_conversation_is_not_exported()
    {
        await using var session = await AssistantTurnTestSession.CreateAsync("consumer", workerEnabled: false);
        var conversation = await session.ConversationAsync();
        await WriteFutureMessageAsync(session, conversation.Id, "Role");

        using var exported = await session.Client.GetAsync(session.Path + $"/conversations/{conversation.Id:D}/export?format=markdown", Ct);
        await AssistantTurnTestSession.AssertProblemAsync(exported, HttpStatusCode.Conflict, AssistantErrors.ConversationUnavailableCode);
    }

    [Fact]
    public async Task An_unreadable_conversation_does_not_admit_a_new_turn()
    {
        await using var session = await AssistantTurnTestSession.CreateAsync("consumer", workerEnabled: false);
        var conversation = await session.ConversationAsync();
        await WriteFutureMessageAsync(session, conversation.Id, "Status");

        using var send = AssistantTurnTestSession.Request(HttpMethod.Post, session.Path + $"/conversations/{conversation.Id:D}/messages",
            AssistantTurnTestSession.Body(conversation), Guid.NewGuid());
        using var response = await session.OpenStreamAsync(send, Ct);
        await AssistantTurnTestSession.AssertProblemAsync(response, HttpStatusCode.Conflict, AssistantErrors.ConversationUnavailableCode);
        using var scope = session.Host.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<AssistantTurnWork>().AsNoTracking()
            .AnyAsync(row => row.OwnerUserId == session.Account.UserId, Ct));
    }

    // Lo que escribiría una versión más nueva: un mensaje válido para esta, con su rol o su estado cambiado en la base.
    private static async Task WriteFutureMessageAsync(AssistantTurnTestSession session, Guid conversationId, string column)
    {
        using var scope = session.Host.Services.CreateScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(session.TenantId!.Value);
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var id = Guid.Empty;
        var written = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            var message = AssistantMessage.Create(conversationId, "User", "Complete", "Mensaje de una versión más nueva", nowUtc);
            services.GetRequiredService<ApplicationDbContext>().Add(message);
            id = message.Id;
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(written.IsSuccess, written.Error.Code);
        var sql = "UPDATE tenant.\"AssistantMessages\" SET \"" + column + "\" = 'Futuro' WHERE \"Id\" = {0}";
        var changed = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            Assert.Equal(1, await services.GetRequiredService<ApplicationDbContext>().Database.ExecuteSqlRawAsync(sql, [id], ct));
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(changed.IsSuccess, changed.Error.Code);
    }
}
