using System.Net;
using System.Net.Http.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantTurns;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

/// <summary>
/// Lo que recibe el proveedor (B14): la hora del pedido con la zona del perfil, al final del último mensaje de la persona,
/// como en el canal (0064 §9, 0054 §1), y nada de otra persona que usa el mismo servidor y el mismo proveedor (216).
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class AssistantRequestContextTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_request_time_in_the_profile_zone_goes_at_the_end_of_the_last_message_of_the_person()
    {
        await using var session = await AssistantTurnTestSession.CreateAsync();
        session.Provider.Release();
        const string prompt = "Consulta con la hora del pedido";
        await CompleteTurnAsync(session.Client, session.Path, prompt);
        var context = Assert.Single(session.Provider.Contexts);
        var asked = context[context.LastIndexOf(prompt, StringComparison.Ordinal)..];
        Assert.Matches(@"^Consulta con la hora del pedido\s*Hora del pedido: [^\n]+, en la zona [^\n]+\.$", asked);
    }

    [Fact]
    public async Task The_request_of_one_person_carries_nothing_of_another()
    {
        await using var session = await AssistantTurnTestSession.CreateAsync();
        session.Provider.Release();
        const string first = "isolated-first-person-private-question";
        await CompleteTurnAsync(session.Client, session.Path, first);

        using var other = AssistantTurnTestSession.NewClient(session.Host);
        var account = await AccountJourney.RegisterAsync(session.Host, other, Ct);
        await AssistantExecutionActorTests.AcceptNoticeAsync(other, session.Path + "/notice");
        const string second = "isolated-second-person-question";
        await CompleteTurnAsync(other, session.Path, second);

        var contexts = session.Provider.Contexts.ToArray();
        Assert.Equal(2, contexts.Length);
        Assert.Contains(first, contexts[0], StringComparison.Ordinal);
        Assert.DoesNotContain(first, contexts[1], StringComparison.Ordinal);
        Assert.DoesNotContain(AssistantTurnTestProvider.Answer, contexts[1], StringComparison.Ordinal);
        Assert.DoesNotContain(session.Account.Email.Value, contexts[1], StringComparison.Ordinal);
        Assert.Contains(second, contexts[1], StringComparison.Ordinal);
        var turns = session.Provider.Invocations.Where(row => row.Owner == account.UserId).ToArray();
        Assert.NotEmpty(turns);
        Assert.All(turns, row => Assert.Equal(account.UserId, row.Owner));
    }

    private static async Task CompleteTurnAsync(HttpClient client, string path, string text)
    {
        using var create = AssistantTurnTestSession.Request(HttpMethod.Post, path + "/conversations", "{}", Guid.NewGuid());
        using var created = await client.SendAsync(create, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var conversation = (await created.Content.ReadFromJsonAsync<AssistantConversationSummaryResponse>(AssistantTurnTestSession.Json, Ct))!;
        using var send = AssistantTurnTestSession.Request(HttpMethod.Post, path + $"/conversations/{conversation.Id:D}/messages",
            AssistantTurnTestSession.Body(conversation, text), Guid.NewGuid());
        using var response = await client.SendAsync(send, HttpCompletionOption.ResponseHeadersRead, Ct).WaitAsync(TimeSpan.FromSeconds(20), Ct);
        using var stream = await AssistantSseTestReader.OpenAsync(response, Ct);
        Assert.Equal("done", (await stream.TerminalAsync(Ct)).Event);
    }
}
