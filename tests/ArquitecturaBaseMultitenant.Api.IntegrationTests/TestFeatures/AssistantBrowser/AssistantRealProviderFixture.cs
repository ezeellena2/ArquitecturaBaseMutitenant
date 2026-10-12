using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using ArquitecturaBaseMultitenant.Api.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Configuration;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantBrowser;

/// <summary>Probe opt-in del proveedor real, con PostgreSQL propio y salida sin contenido privado.</summary>
public static class AssistantRealProviderFixture
{
    private const string Root = "/api/organization/assistant";
    private const string OperatorEmail = "assistant-real-operator@example.test";
    internal const string KeyName = "Assistant:Platform:ApiKey";
    private static readonly JsonSerializerOptions Json = CreateJson();

    public static async Task<int> RunAsync(string exportDirectory, CancellationToken ct)
    {
        try
        {
            Require(Environment.GetEnvironmentVariable("MT_E2E_ISOLATED") == "1", "IsolatedRequired");
            Require(Path.IsPathFullyQualified(exportDirectory), "AbsoluteExportsRequired");
            var configuration = new ConfigurationBuilder().AddUserSecrets(typeof(Program).Assembly, optional: true).Build();
            var options = HostSettings(configuration);
            var secret = options[KeyName]!;
            await using var factory = new ApiFactory();
            await factory.InitializeDatabaseAsync(ct);
            await using var host = factory.WithWebHostBuilder(builder => builder
                .UseSetting("Assistant:TurnWorkerEnabled", "true")
                .UseSetting("Seed:PlatformOwner:Email", OperatorEmail)
                .ConfigureLogging(logging => logging.ClearProviders())
                .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(options)));
            var configured = host.Services.GetRequiredService<IOptions<AssistantOptions>>().Value;
            Require(!configured.UseFakeChatClient, "FakeForbidden");
            Require(configured.TurnWorkerEnabled, "WorkerRequired");
            using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
            var owner = await AccountJourney.RegisterAsync(host, client, ct);
            var tenantId = await OrganizationJourney.StageAsync(factory, owner, "Organización del probe real", ct);
            await AccountJourney.AuthorizeAsync(client, "business", ct, tenantId);
            await AcceptCurrentLegalAsync(client, ct);
            using var operatorClient = await LoginOperatorAsync(host, ct);

            AssistantModelPriceResponse model;
            using (var scope = host.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                Require(services.GetRequiredService<IChatClientFactory>() is AnthropicChatClientFactory, "NativeFactoryRequired");
                var models = await services.GetRequiredService<IAssistantModelReader>().ListAllAsync(ct);
                model = await AvailableModelAsync(secret, models, ct) ?? throw new ProbeFailure("NoAvailableCatalogModel");
                await AssistantFileBridge.WriteAsync(services, async token =>
                {
                    var db = services.GetRequiredService<ApplicationDbContext>();
                    foreach (var row in await db.Set<AssistantModelPrice>().ToArrayAsync(token))
                        row.Update(row.Provider, row.ProviderModelId, row.DisplayName, row.InputPrice, row.OutputPrice, row.Id == model.Id);
                }, ct);
            }

            // El control existente configura datos de escenario; la clave sigue exclusivamente en opciones.
            var directory = Directory.CreateTempSubdirectory("mt-e2e-assistant-real-").FullName;
            try
            {
                var ready = Path.Combine(directory, "mt-e2e-assistant-real.ready");
                var bridge = new AssistantFileBridge(host.Services, ready, isolated: true);
                await CommandAsync(bridge, ready, "prepare", new { email = owner.Email.Value, access = "business", tenantId }, ct);
                using var noticeState = await ReadAsync(client, Root, ct);
                using (var accepted = await PostAsync(client, Root + "/notice", new { noticeVersion = noticeState.RootElement.GetProperty("noticeVersion").GetString() }, ct))
                    Require(accepted.StatusCode == HttpStatusCode.NoContent, "NoticeAcceptance");

                // Ingreso, permisos, política y destinatario se comprueban antes del primer turno generativo.
                var policyRoute = $"/api/platform/tenants/{tenantId:D}/assistant/policy";
                using var policy = await ReadAsync(operatorClient, policyRoute, ct);
                using (var allowed = await operatorClient.PutAsJsonAsync(policyRoute,
                    new { allowOwnKey = true, reason = "Probe aislado del proveedor real", version = policy.RootElement.GetProperty("version").GetUInt32() }, ct))
                    Require(allowed.StatusCode == HttpStatusCode.OK, "OwnKeyPolicy");
                using var settings = await ReadAsync(client, Root + "/settings", ct);
                Require(settings.RootElement.GetProperty("hasAlertRecipient").GetBoolean(), "VerifiedOwnerRequired");
                Report(new { stage = "preflight", status = "ready", operatorPermissions = true, verifiedOwnerRecipient = true });

                var multimodal = await AssistantRealProviderScenario.CompleteAsync(client, owner.UserId,
                    "Usá una sola vez org_users para consultar usuarios. Leé también el PDF y la imagen adjuntos. Respondé en hasta tres renglones: cantidad de usuarios, código del PDF y color de la imagen.",
                    attachments: true, stop: false, ct);
                Require(multimodal.ToolCompleted && multimodal.Text.Contains(AssistantRealProviderAssets.PdfMarker, StringComparison.Ordinal)
                    && (multimodal.Text.Contains("rojo", StringComparison.OrdinalIgnoreCase) || multimodal.Text.Contains("red", StringComparison.OrdinalIgnoreCase)), "ToolAndMultimodalAnswer");
                await AssistantRealProviderScenario.ExportAsync(client, multimodal.ConversationId, exportDirectory, ct);
                var platformCalls = await LedgerAsync(host.Services, owner.UserId, tenantId, [multimodal.OperationId], AssistantKeyFunding.PlatformKey, ct);

                using (var saved = await client.PutAsJsonAsync(Root + "/settings", new
                {
                    mode = "OwnKey", modelId = model.Id, effort = "low", dailyMessageLimit = 50,
                    monthlyBudget = new { amount = 10m, currency = "USD" }, keyAction = "replace", apiKey = secret,
                    version = settings.RootElement.GetProperty("version").GetUInt32(),
                }, ct)) Require(saved.StatusCode == HttpStatusCode.OK, "OwnKeyValidationAndSave");
                using var ownRead = await ReadAsync(client, Root + "/settings", ct);
                Require(ownRead.RootElement.GetProperty("mode").GetString() == "OwnKey"
                    && ownRead.RootElement.GetProperty("hasKey").GetBoolean()
                    && !ownRead.RootElement.GetRawText().Contains(secret, StringComparison.Ordinal), "WriteOnlyKey");

                // El saldo anterior es explícitamente sintético; la llamada actual y su finalización son reales.
                await CommandAsync(bridge, ready, "quotas", new { email = owner.Email.Value, access = "business", tenantId, remainingMessages = 50, monthlyFraction = .8m }, ct);
                var own = await AssistantRealProviderScenario.CompleteAsync(client, owner.UserId,
                    "Respondé sólo: Verificación del proveedor real completada.", attachments: false, stop: false, ct);
                await AssertAlertAsync(host.Services, tenantId, 80, ct);
                await CommandAsync(bridge, ready, "quotas", new { email = owner.Email.Value, access = "business", tenantId, remainingMessages = 50, monthlyFraction = 1m, notify = true }, ct);
                await AssertAlertAsync(host.Services, tenantId, 100, ct);
                var beforeBlocked = await CallCountAsync(host.Services, owner.UserId, tenantId, ct);
                await AssistantRealProviderScenario.AssertBudgetBlockedAsync(client, ct);
                Require(beforeBlocked == await CallCountAsync(host.Services, owner.UserId, tenantId, ct), "BudgetNoProviderCall");
                await CommandAsync(bridge, ready, "quotas", new { email = owner.Email.Value, access = "business", tenantId, remainingMessages = 50, monthlyFraction = 0m }, ct);
                var stopped = await AssistantRealProviderScenario.CompleteAsync(client, owner.UserId,
                    "Escribí una lista larga de 600 elementos numerados, cada uno con una frase distinta sobre organización del trabajo. No uses herramientas.", attachments: false, stop: true, ct);
                var ownCalls = await LedgerAsync(host.Services, owner.UserId, tenantId, [own.OperationId, stopped.OperationId], AssistantKeyFunding.OwnKey, ct);
                Require(platformCalls + ownCalls <= 8, "ProviderCallBudget");
                using var usage = await ReadAsync(client, Root + "/usage?pageSize=10", ct);
                Require(usage.RootElement.TryGetProperty("items", out _), "UsageRead");
                Report(new { stage = "complete", status = "passed", responseAndTitleCalls = platformCalls + ownCalls, keyValidationCalls = 1, syntheticPriorBalance = true });
                return 0;
            }
            finally
            {
                Require(Path.GetDirectoryName(directory) == Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)
                    && Path.GetFileName(directory).StartsWith("mt-e2e-assistant-real-", StringComparison.Ordinal), "OwnedTemporaryDirectory");
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (ProbeFailure failure) { Report(new { status = "failed", code = failure.Code }); return 1; }
        catch (AnthropicUnauthorizedException) { Report(new { status = "failed", httpStatus = 401 }); return 1; }
        catch (AnthropicForbiddenException) { Report(new { status = "failed", httpStatus = 403 }); return 1; }
        catch (AnthropicRateLimitException) { Report(new { status = "failed", httpStatus = 429 }); return 1; }
        catch (Exception error) { Report(new { status = "failed", type = error.GetType().Name }); return 1; }
    }

    internal static Dictionary<string, string?> HostSettings(IConfiguration configuration)
    {
        var key = configuration[KeyName];
        Require(!string.IsNullOrWhiteSpace(key), "ConfiguredKeyRequired");
        return new()
        {
            [KeyName] = key, ["Assistant:UseFakeChatClient"] = "false", ["Assistant:IsolatedE2E"] = "true",
            ["Assistant:TurnWorkerEnabled"] = "true", ["Seed:PlatformOwner:Email"] = OperatorEmail,
            ["Seed:PlatformOwner:Phone"] = "", ["Seed:PlatformOwner:FirstName"] = "Operador", ["Seed:PlatformOwner:LastName"] = "Probe",
        };
    }
    private static async Task<HttpClient> LoginOperatorAsync(WebApplicationFactory<Program> host, CancellationToken ct)
    {
        // InitializeDatabaseAsync sólo bootstrap/migra/referencias. RegisterAsync ya ejecutó SeedDatabaseAsync
        // sobre este host con OperatorEmail, antes de abrir el host base de OrganizationJourney.
        using (var scope = host.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var db = services.GetRequiredService<ApplicationDbContext>();
            var operatorId = await db.LoginMethods.Where(row => row.Value == OperatorEmail && row.VerifiedAtUtc != null)
                .Select(row => row.UserId).SingleAsync(ct);
            Require(await db.Users.AnyAsync(row => row.Id == operatorId && row.IsPlatformOperator, ct), "ExclusiveOperatorSeed");
            Require((await services.GetRequiredService<IUserTenantAccessReader>().ListForUserAsync(operatorId, ct)).Count == 0, "OperatorHasNoBusinessOrConsumerAccess");
        }
        var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        try
        {
            using (var requested = await AccountJourney.PostAsync(client, "/api/auth/login-code", new { email = OperatorEmail }, ct))
                Require(requested.StatusCode == HttpStatusCode.Accepted, "OperatorLoginRequest");
            var code = await PickupCodeReader.ReadAsync(host.Services, OperatorEmail, ct);
            using (var verified = await AccountJourney.PostAsync(client, "/api/auth/login-code/verify",
                new { email = OperatorEmail, code, returnUrl = "/connect/authorize?access=platform" }, ct))
                Require(verified.StatusCode == HttpStatusCode.OK, "OperatorLogin");
            await AccountJourney.AuthorizeAsync(client, "platform", ct);
            await AcceptCurrentLegalAsync(client, ct);
            using var management = await ReadAsync(client, "/api/platform/assistant/settings", ct);
            return client;
        }
        catch { client.Dispose(); throw; }
    }
    private static async Task AcceptCurrentLegalAsync(HttpClient client, CancellationToken ct)
    {
        using var me = await ReadAsync(client, "/api/me", ct);
        var documents = me.RootElement.GetProperty("pendingLegalDocuments").EnumerateArray()
            .Select(row => new { id = row.GetProperty("id").GetGuid(), version = row.GetProperty("version").GetInt32() }).ToArray();
        if (documents.Length == 0) return;
        using var accepted = await PostAsync(client, "/api/legal/accept", new { documents }, ct);
        Require(accepted.StatusCode == HttpStatusCode.NoContent, "CurrentLegalAcceptance");
    }
    private static async Task<AssistantModelPriceResponse?> AvailableModelAsync(string key, IReadOnlyList<AssistantModelPriceResponse> catalog, CancellationToken ct)
    {
        using var provider = new AnthropicClient { ApiKey = key, MaxRetries = 0, Timeout = TimeSpan.FromSeconds(30) };
        foreach (var model in catalog.Where(row => AssistantModelCompatibilityPolicy.Supports(row, AssistantEffort.Low)
            && row.Technical is { SupportsImages: true, SupportsPdf: true }).OrderBy(row => row.InputPricePerMillion.Amount + row.OutputPricePerMillion.Amount))
        {
            try
            {
                var resolved = await provider.Models.Retrieve(model.ProviderModelId, cancellationToken: ct);
                var metadata = JsonSerializer.SerializeToElement(resolved);
                if (metadata.TryGetProperty("lifecycle", out var lifecycle) && lifecycle.GetString() == "retired") continue;
                Report(new { stage = "availability", model = model.ProviderModelId, status = "available" });
                return model;
            }
            catch (AnthropicNotFoundException) { Report(new { stage = "availability", model = model.ProviderModelId, status = "not-found" }); }
        }
        var listed = await provider.Models.List(cancellationToken: ct);
        Report(new { stage = "availability", status = "no-compatible-catalog-model", providerIds = listed.Items.Select(row => row.ID).ToArray() });
        return null;
    }
    private static async Task<int> LedgerAsync(IServiceProvider root, Guid ownerId, Guid tenantId, Guid[] operationIds, AssistantKeyFunding funding, CancellationToken ct)
    {
        using var scope = root.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // Inventario GlobalEntities/platform: mt_app puede leer contabilidad técnica sin filtros de tenant.
        // Se conserva igual el binding del actor que ingresó por HTTP; no se leen hechos privados.
        var operations = await db.Set<AssistantFinancialOperation>().Where(row => operationIds.Contains(row.Id)
            && row.UserId == ownerId && row.Access == Access.Business && row.ScopeTenantId == tenantId).ToArrayAsync(ct);
        Require(operations.Length == operationIds.Length && operations.All(row => row.UserId == ownerId && row.Funding == funding && row.State == AssistantFinancialState.Completed), "DurableFunding");
        var calls = await db.Set<AssistantFinancialCall>().Where(row => operationIds.Contains(row.OperationId)).ToArrayAsync(ct);
        Require(calls.All(row => row.IsFinal && row.InputTokens >= 0 && row.OutputTokens >= 0), "FinalUsage");
        Require(calls.Count(row => row.IsTitle) == 1, "SingleCompletedTitle");
        Report(new { stage = "ledger", funding = funding.ToString(), calls = calls.Length, titleCalls = calls.Count(row => row.IsTitle), inputTokens = calls.Sum(row => row.InputTokens), outputTokens = calls.Sum(row => row.OutputTokens), recordedCostUsd = calls.Sum(row => row.Cost.Amount), estimatedCalls = calls.Count(row => row.IsEstimated) });
        return calls.Length;
    }
    private static async Task<int> CallCountAsync(IServiceProvider root, Guid ownerId, Guid tenantId, CancellationToken ct)
    {
        using var scope = root.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await (from call in db.Set<AssistantFinancialCall>() join operation in db.Set<AssistantFinancialOperation>() on call.OperationId equals operation.Id
            where operation.UserId == ownerId && operation.Access == Access.Business && operation.ScopeTenantId == tenantId select call.Id).CountAsync(ct);
    }
    private static async Task AssertAlertAsync(IServiceProvider root, Guid tenantId, int threshold, CancellationToken ct)
    {
        using var scope = root.CreateScope();
        var alerts = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<AssistantBudgetAlert>()
            .Where(row => row.OrganizationId == tenantId && row.Funding == AssistantKeyFunding.OwnKey && row.Threshold == threshold).ToArrayAsync(ct);
        Require(alerts.Length == 1 && alerts[0].EnqueuedAtUtc is not null, "BudgetAlertOutbox");
        Report(new { stage = "alert", threshold, status = "enqueued", priorBalance = "synthetic" });
    }
    private static async Task CommandAsync(AssistantFileBridge bridge, string ready, string kind, object args, CancellationToken ct)
    {
        var command = Path.ChangeExtension(ready, ".assistant.json");
        var id = Guid.NewGuid();
        await File.WriteAllTextAsync(command, JsonSerializer.Serialize(new { id, kind, args }), ct);
        Require(await bridge.TryExecuteAsync(ct), "ScenarioCommand");
        using var reply = JsonDocument.Parse(await File.ReadAllTextAsync(command + ".done", ct));
        Require(reply.RootElement.GetProperty("ok").GetBoolean(), "ScenarioCommandState");
        File.Delete(command); File.Delete(command + ".done");
    }
    internal static async Task<JsonDocument> ReadAsync(HttpClient client, string route, CancellationToken ct)
    {
        using var response = await client.GetAsync(route, ct);
        Require(response.StatusCode == HttpStatusCode.OK, "ReadHttp");
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }
    internal static async Task<HttpResponseMessage> PostAsync(HttpClient client, string route, object value, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json") };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        return await client.SendAsync(request, ct);
    }
    internal static void Require(bool condition, string code) { if (!condition) throw new ProbeFailure(code); }
    internal static void Report(object value) => Console.WriteLine("ANTHROPIC " + JsonSerializer.Serialize(value));
    internal static JsonSerializerOptions JsonOptions => Json;
    private static JsonSerializerOptions CreateJson() { var result = new JsonSerializerOptions(JsonSerializerDefaults.Web); JsonConfiguration.ConfigureJson(result); return result; }
    internal sealed class ProbeFailure(string code) : Exception("Assistant real probe failed.") { internal string Code { get; } = code; }
}
