using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantTurns;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

/// <summary>
/// ADR 0064 §6 y §7 (B13), con la API, el worker y PostgreSQL reales y solo el transporte del proveedor de prueba: quién ve qué según
/// quién paga, la falla que queda en la configuración hasta que el proveedor vuelve a responder, la clave ilegible que no gasta y el
/// proveedor desconocido que pausa.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class AssistantProviderIssueTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task With_the_platform_key_the_person_sees_unavailable_and_the_operator_keeps_the_issue_until_the_provider_answers()
    {
        await using var session = await AssistantTurnTestSession.CreateAsync("business");
        session.Provider.Transport = new StatusTransport(HttpStatusCode.Unauthorized);
        Assert.Equal(AssistantErrors.ProviderPlatformUnavailableCode, await FailedTurnAsync(session));
        await WaitForIssueAsync(session, AssistantErrors.ProviderCredentialRejectedCode);
        Assert.Equal(AssistantErrors.ProviderPlatformUnavailableCode, await OrganizationIssueAsync(session));

        // A 5xx may have started generating: the person sees it as uncertain and the operator's issue does not change.
        session.Provider.Transport = new StatusTransport((HttpStatusCode)529);
        Assert.Equal(AssistantErrors.ProviderUncertainCode, await FailedTurnAsync(session));
        Assert.Equal(AssistantErrors.ProviderCredentialRejectedCode, await PlatformIssueAsync(session));

        session.Provider.Transport = null;
        session.Provider.Release();
        var conversation = await session.ConversationAsync();
        Assert.Equal("done", (await TerminalAsync(session, conversation)).Event);
        await WaitForIssueAsync(session, null);
        Assert.Null(await OrganizationIssueAsync(session));

        // Without a connection nothing left: the person may retry, the operator sees it, and the marked call costs nothing.
        session.Provider.Transport = new StatusTransport(null);
        var refused = await TerminalAsync(session, await session.ConversationAsync());
        Assert.Equal(AssistantErrors.ProviderUnavailableCode, refused.Data.GetProperty("code").GetString());
        await WaitForIssueAsync(session, AssistantErrors.ProviderUnavailableCode);
        await using var scope = session.Host.Services.CreateAsyncScope();
        var turnId = refused.Data.GetProperty("turnId").GetGuid();
        var call = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<AssistantFinancialCall>()
            .SingleAsync(row => row.OperationId == turnId && !row.IsTitle, Ct);
        Assert.Equal((0m, false), (call.Cost.Amount, call.IsEstimated));
    }

    [Fact]
    public async Task An_unreadable_platform_key_leaves_the_assistant_unavailable_without_reserving_quota_or_calling_the_provider()
    {
        await using var session = await AssistantTurnTestSession.CreateAsync();
        var conversation = await session.ConversationAsync();
        await ChangePlatformAsync(session, settings => settings.ReplaceKey([1, 2, 3, 4], "1234"));
        using (var state = await session.Client.GetAsync(session.Path, Ct))
        {
            Assert.Equal(HttpStatusCode.OK, state.StatusCode);
            using var value = JsonDocument.Parse(await state.Content.ReadAsStringAsync(Ct));
            Assert.False(value.RootElement.GetProperty("configured").GetBoolean());
        }
        using var send = AssistantTurnTestSession.Request(HttpMethod.Post, session.Path + $"/conversations/{conversation.Id:D}/messages",
            AssistantTurnTestSession.Body(conversation), Guid.NewGuid());
        using var response = await session.Client.SendAsync(send, Ct);
        await AssistantTurnTestSession.AssertProblemAsync(response, HttpStatusCode.BadRequest, AssistantErrors.SettingsNotConfiguredCode);
        using var scope = session.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.Set<AssistantFinancialOperation>().AnyAsync(row => row.UserId == session.Account.UserId, Ct));
        Assert.False(await db.Set<AssistantPlatformKeyDailyQuota>().AnyAsync(row => row.UserId == session.Account.UserId
            && (row.ReservedQueries > 0 || row.ConsumedQueries > 0), Ct));
        Assert.Equal(0, session.Provider.Calls);
    }

    [Fact]
    public async Task A_provider_this_version_does_not_know_pauses_the_platform_and_saving_without_touching_the_key_discards_it()
    {
        await using var session = await AssistantTurnTestSession.CreateAsync("platform");
        using (var saved = await session.Client.PutAsJsonAsync("/api/platform/assistant/settings",
            Body(await PlatformSettingsAsync(session), "replace", "isolated-unsupported-9999"), Ct))
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var known = (await PlatformSettingsAsync(session)).RootElement.GetProperty("defaultModelId").GetGuid();
        var future = AssistantModelPrice.Create("FutureProvider", "isolated-future-model", "Modelo futuro",
            new Money(1m, CurrencyCode.Create("USD").Value), new Money(2m, CurrencyCode.Create("USD").Value));
        await ChangePlatformAsync(session, settings =>
        {
            settings.Update(future.Id, [.. settings.AllowedModelIds, future.Id], settings.Effort, settings.DailyMessageLimit,
                settings.MonthlyOrganizationBudget, settings.PersonalEnabled, settings.RetentionDays);
        }, future);
        await RecordPlatformIssueAsync(session, AssistantErrors.ProviderLimitedCode);

        using (var paused = await PlatformSettingsAsync(session))
        {
            Assert.True(paused.RootElement.GetProperty("unsupportedProvider").GetBoolean());
            Assert.Equal(AssistantErrors.ProviderLimitedCode, paused.RootElement.GetProperty("providerIssue").GetString());
            Assert.True(paused.RootElement.GetProperty("hasKey").GetBoolean());
            // The future model is never offered nor read as a known one.
            using var models = JsonDocument.Parse(await session.Client.GetStringAsync("/api/platform/assistant/models?page=1&pageSize=100", Ct));
            Assert.DoesNotContain(models.RootElement.GetProperty("items").EnumerateArray(), model => model.GetProperty("id").GetGuid() == future.Id);
        }
        using (var state = JsonDocument.Parse(await session.Client.GetStringAsync(session.Path, Ct)))
            Assert.False(state.RootElement.GetProperty("configured").GetBoolean());

        var body = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(Body(await PlatformSettingsAsync(session), "keep")))!;
        body["defaultModelId"] = known;
        body["allowedModelIds"] = new System.Text.Json.Nodes.JsonArray(known);
        using (var discarded = await session.Client.PutAsJsonAsync("/api/platform/assistant/settings", body, Ct))
            Assert.Equal(HttpStatusCode.OK, discarded.StatusCode);
        using var after = await PlatformSettingsAsync(session);
        Assert.False(after.RootElement.GetProperty("unsupportedProvider").GetBoolean());
        Assert.False(after.RootElement.GetProperty("hasKey").GetBoolean());
        Assert.Equal(JsonValueKind.Null, after.RootElement.GetProperty("providerIssue").ValueKind);
    }

    [Fact]
    public async Task With_the_own_key_the_issue_stays_private_to_the_organization_and_never_changes_its_settings_version()
    {
        await using var session = await AssistantTurnTestSession.CreateAsync("business");
        var tenantId = session.TenantId!.Value;
        // A business session can't read the platform settings over HTTP: the model comes from the stored row.
        Guid platform;
        await using (var scope = session.Host.Services.CreateAsyncScope())
            platform = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<AssistantPlatformSettings>().AsNoTracking()
                .Select(row => row.DefaultModelId).SingleAsync(Ct);
        var version = await InOrganizationAsync(session, tenantId, async services =>
        {
            var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
            {
                var settings = AssistantOrganizationSettings.Create();
                settings.Update(nameof(Application.Modules.Assistant.Models.AssistantMode.OwnKey), platform, "Low", 50, null, 30);
                services.GetRequiredService<ApplicationDbContext>().Add(settings);
                return Task.FromResult(Result.Success(settings));
            }, CommitPolicy.OnSuccess, Ct);
            Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : "");
            return result.Value.Version;
        });

        // What the provider said is recorded for the organization that pays, in its own private row.
        Assert.Equal(AssistantErrors.ProviderLimitedCode, await RecordOwnAsync(session, tenantId, AssistantErrors.ProviderLimitedCode, version));
        // A turn that built its client before the Dueño saved again answers about the previous settings: it is not recorded.
        Assert.Equal(AssistantErrors.ProviderLimitedCode, await RecordOwnAsync(session, tenantId, AssistantErrors.ProviderCredentialRejectedCode, version + 1));
        await InOrganizationAsync(session, tenantId, async services =>
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(version, await db.Set<AssistantOrganizationSettings>().AsNoTracking().Select(row => row.Version).SingleAsync(Ct));
            Assert.Equal(tenantId, await db.Set<AssistantOrganizationProviderIssue>().AsNoTracking().Select(row => row.TenantId).SingleAsync(Ct));
            return 0u;
        });
        // Another organization never sees it.
        await InOrganizationAsync(session, Guid.CreateVersion7(), async services =>
        {
            Assert.False(await services.GetRequiredService<ApplicationDbContext>().Set<AssistantOrganizationProviderIssue>().AnyAsync(Ct));
            return 0u;
        });
        Assert.Null(await RecordOwnAsync(session, tenantId, null, version));
    }

    [Fact]
    public async Task The_installed_transport_gives_the_provider_ten_seconds_to_connect()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        var handler = factory.Services.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(AnthropicChatClientBuilder.TransportName);
        var chain = new List<HttpMessageHandler>();
        for (HttpMessageHandler? current = handler; current is not null; current = (current as DelegatingHandler)?.InnerHandler) chain.Add(current);
        Assert.Contains(chain, value => value is AssistantProviderConnectHandler);
        var primary = Assert.IsType<SocketsHttpHandler>(chain[^1]);
        Assert.Equal(TimeSpan.FromSeconds(10), primary.ConnectTimeout);
        // A redirect would send the paid request again, with x-api-key, to another host: it stays a rejection.
        Assert.False(primary.AllowAutoRedirect);
    }

    private static async Task<string?> FailedTurnAsync(AssistantTurnTestSession session)
    {
        var terminal = await TerminalAsync(session, await session.ConversationAsync());
        Assert.Equal("error", terminal.Event);
        return terminal.Data.GetProperty("code").GetString();
    }

    private static async Task<AssistantSseTestReader.Frame> TerminalAsync(AssistantTurnTestSession session,
        Application.Modules.Assistant.Models.AssistantConversationSummaryResponse conversation)
    {
        using var send = AssistantTurnTestSession.Request(HttpMethod.Post, session.Path + $"/conversations/{conversation.Id:D}/messages",
            AssistantTurnTestSession.Body(conversation), Guid.NewGuid());
        using var response = await session.OpenStreamAsync(send, Ct);
        using var stream = await AssistantSseTestReader.OpenAsync(response, Ct);
        return await stream.TerminalAsync(Ct);
    }

    private static async Task<string?> OrganizationIssueAsync(AssistantTurnTestSession session)
    {
        using var settings = JsonDocument.Parse(await session.Client.GetStringAsync("/api/organization/assistant/settings", Ct));
        var issue = settings.RootElement.GetProperty("providerIssue");
        return issue.ValueKind == JsonValueKind.Null ? null : issue.GetString();
    }

    private static async Task<string?> PlatformIssueAsync(AssistantTurnTestSession session)
    {
        await using var scope = session.Host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<AssistantPlatformProviderIssue>().AsNoTracking()
            .Select(row => row.Code).SingleOrDefaultAsync(Ct);
    }

    /// <summary>The worker records the outcome right after closing the turn, outside its transaction.</summary>
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

    private static async Task ChangePlatformAsync(AssistantTurnTestSession session, Action<AssistantPlatformSettings> change, AssistantModelPrice? model = null)
    {
        await using var scope = session.Host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            if (model is not null) db.Add(model);
            change(await db.Set<AssistantPlatformSettings>().SingleAsync(ct));
            services.GetRequiredService<ISecurityEventRepository>().Add(SecurityEvent.ForPlatformSettings(AuditActorKind.System, Guid.Empty,
                "Assistant provider issue fixture", services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : "");
    }

    private static async Task<T> InOrganizationAsync<T>(AssistantTurnTestSession session, Guid tenantId, Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = session.Host.Services.CreateAsyncScope();
        using var entered = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
        return await work(scope.ServiceProvider);
    }

    /// <summary>Lo que hace el worker después de un turno con la clave propia, y lo que queda anotado.</summary>
    private static Task<string?> RecordOwnAsync(AssistantTurnTestSession session, Guid tenantId, string? code, uint settingsVersion) =>
        InOrganizationAsync(session, tenantId, async services =>
        {
            var recorded = await services.GetRequiredService<Application.Modules.Assistant.Interfaces.Services.IAssistantSettingsService>()
                .RecordProviderOutcomeAsync(Application.Modules.Assistant.Models.AssistantMode.OwnKey, code, settingsVersion, Ct);
            Assert.True(recorded.IsSuccess, recorded.IsFailure ? recorded.Error.Code : "");
            return (await services.GetRequiredService<Application.Modules.Assistant.Interfaces.Persistence.IAssistantSettingsReader>()
                .FindOrganizationAsync(Ct))?.ProviderIssueCode;
        });

    /// <summary>La falla anotada en su propia fila, como la deja el worker: no cambia la configuración ni pide un evento.</summary>
    private static async Task RecordPlatformIssueAsync(AssistantTurnTestSession session, string code)
    {
        await using var scope = session.Host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            var issue = await db.Set<AssistantPlatformProviderIssue>().SingleOrDefaultAsync(ct);
            if (issue is null) db.Add(issue = AssistantPlatformProviderIssue.Create());
            issue.Record(code, services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : "");
    }

    /// <summary>El proveedor de prueba responde siempre el mismo estado, con un cuerpo que nunca debe leerse; sin estado, no conecta.</summary>
    private sealed class StatusTransport(HttpStatusCode? status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            status is null ? throw new HttpRequestException(HttpRequestError.ConnectionError, "isolated-refused-connection") : Task.FromResult(new HttpResponseMessage(status.Value)
            {
                Content = new StringContent("""{"type":"error","error":{"type":"error","message":"isolated-sensitive-provider-payload"}}""",
                    Encoding.UTF8, "application/json"),
            });
    }
}
