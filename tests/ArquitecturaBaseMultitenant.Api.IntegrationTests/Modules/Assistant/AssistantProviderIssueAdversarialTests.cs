using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantTurns;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

/// <summary>
/// Revisión adversarial de la B13, con la API, el worker y PostgreSQL reales y solo el transporte del proveedor de prueba: lo que
/// el worker escribe en la configuración de quien paga después de cada turno, y cómo se clasifica un pedido que quedó en vuelo.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class AssistantProviderIssueAdversarialTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Recording_the_provider_issue_does_not_make_the_open_settings_screen_conflict()
    {
        await using var session = await AssistantTurnTestSession.CreateAsync("platform");
        // The operator opens «Configuración del asistente»: the screen keeps this version to save.
        using var opened = await PlatformSettingsAsync(session);

        // Meanwhile a turn hits the provider's limit and the worker records it in the same row.
        session.Provider.Transport = new StatusTransport(HttpStatusCode.TooManyRequests);
        await FailedTurnAsync(session);
        await WaitForIssueAsync(session, AssistantErrors.ProviderLimitedCode);

        // Nobody else saved: the operator's save must not get «someone changed it».
        using var saved = await session.Client.PutAsJsonAsync("/api/platform/assistant/settings", Body(opened, "keep"), Ct);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
    }

    [Fact]
    public async Task A_failure_of_a_turn_that_used_the_replaced_key_does_not_mark_the_new_key()
    {
        await using var session = await AssistantTurnTestSession.CreateAsync("platform");
        var transport = new GatedTransport(HttpStatusCode.Unauthorized);
        session.Provider.Transport = transport;
        var conversation = await session.ConversationAsync();
        var inFlight = TerminalAsync(session, conversation);
        await transport.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30), Ct);

        // The operator replaces the key while that turn is still waiting on the provider with the previous one.
        using (var opened = await PlatformSettingsAsync(session))
        using (var replaced = await session.Client.PutAsJsonAsync("/api/platform/assistant/settings",
            Body(opened, "replace", "isolated-new-platform-key-5678"), Ct))
            Assert.Equal(HttpStatusCode.OK, replaced.StatusCode);

        // The provider rejects the previous key: that answer is about the key that is no longer saved.
        transport.Gate.TrySetResult();
        Assert.Equal("error", (await inFlight).Event);
        for (var attempt = 0; attempt < 120; attempt++)
        {
            Assert.Null(await PlatformIssueAsync(session));
            await Task.Delay(25, Ct);
        }
    }

    [Fact]
    public async Task A_call_cut_by_the_turn_deadline_while_waiting_on_the_provider_is_uncertain_not_unavailable()
    {
        await using var session = await AssistantTurnTestSession.CreateAsync("platform",
            configure: builder => builder.UseSetting("Assistant:TurnTimeout", "00:00:03"));
        // The request reached the provider, which is still generating when the turn's deadline arrives.
        session.Provider.Transport = new HangingTransport();
        var terminal = await TerminalAsync(session, await session.ConversationAsync());
        Assert.Equal("error", terminal.Event);

        await using (var scope = session.Host.Services.CreateAsyncScope())
        {
            var turnId = terminal.Data.GetProperty("turnId").GetGuid();
            var call = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<AssistantFinancialCall>()
                .SingleAsync(row => row.OperationId == turnId && !row.IsTitle, Ct);
            // It is charged as sent (0064 §1: what reserved a step counts even if it ends uncertain)...
            Assert.True(call.IsEstimated || call.Cost.Amount > 0m);
        }
        // ...so the person must be told it may have reached the provider, not «Unavailable: nothing was sent».
        Assert.Equal(AssistantErrors.ProviderUncertainCode, terminal.Data.GetProperty("code").GetString());
    }

    private static async Task FailedTurnAsync(AssistantTurnTestSession session) =>
        Assert.Equal("error", (await TerminalAsync(session, await session.ConversationAsync())).Event);

    private static async Task<AssistantSseTestReader.Frame> TerminalAsync(AssistantTurnTestSession session,
        Application.Modules.Assistant.Models.AssistantConversationSummaryResponse conversation)
    {
        using var send = AssistantTurnTestSession.Request(HttpMethod.Post, session.Path + $"/conversations/{conversation.Id:D}/messages",
            AssistantTurnTestSession.Body(conversation), Guid.NewGuid());
        using var response = await session.OpenStreamAsync(send, Ct);
        using var stream = await AssistantSseTestReader.OpenAsync(response, Ct);
        return await stream.TerminalAsync(Ct);
    }

    private static async Task<string?> PlatformIssueAsync(AssistantTurnTestSession session)
    {
        await using var scope = session.Host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<AssistantPlatformProviderIssue>().AsNoTracking()
            .Select(row => row.Code).SingleOrDefaultAsync(Ct);
    }

    private static async Task WaitForIssueAsync(AssistantTurnTestSession session, string? code)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            if (await PlatformIssueAsync(session) == code) return;
            await Task.Delay(25, Ct);
        }
        Assert.Fail($"The platform settings did not record {code ?? "the cleared issue"}.");
    }

    private static async Task<JsonDocument> PlatformSettingsAsync(AssistantTurnTestSession session) =>
        JsonDocument.Parse(await session.Client.GetStringAsync("/api/platform/assistant/settings", Ct));

    private static object Body(JsonDocument settings, string keyAction, string? apiKey = null) => new
    {
        defaultModelId = settings.RootElement.GetProperty("defaultModelId").GetGuid(),
        allowedModelIds = settings.RootElement.GetProperty("allowedModelIds").EnumerateArray().Select(value => value.GetGuid()).ToArray(),
        effort = "low",
        dailyMessageLimit = 50,
        monthlyOrganizationBudget = new { amount = 1000m, currency = "USD" },
        personalEnabled = true,
        retentionDays = 30,
        keyAction,
        apiKey,
        version = settings.RootElement.GetProperty("version").GetUInt32(),
    };

    private static HttpResponseMessage Error(HttpStatusCode status) => new(status)
    {
        Content = new StringContent("""{"type":"error","error":{"type":"error","message":"isolated-sensitive-provider-payload"}}""",
            Encoding.UTF8, "application/json"),
    };

    private sealed class StatusTransport(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(Error(status));
    }

    /// <summary>Recibe el pedido y espera la señal de la prueba antes de responder el estado.</summary>
    private sealed class GatedTransport(HttpStatusCode status) : HttpMessageHandler
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Entered.TrySetResult();
            await Gate.Task.WaitAsync(cancellationToken);
            return Error(status);
        }
    }

    /// <summary>El proveedor recibió el pedido y no termina de responder.</summary>
    private sealed class HangingTransport : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Error(HttpStatusCode.OK);
        }
    }
}
