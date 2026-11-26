using System.Runtime.CompilerServices;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Actions;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;
using ArquitecturaBaseMultitenant.Application.Services.Roles;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantBrowser;

/// <summary>Doble determinista instalado sólo por ConfigureTestServices.</summary>
internal sealed class FakeChatClient(AssistantFakeChatMetrics metrics, TimeProvider clock) : IChatClient
{
    private static readonly System.Resources.ResourceManager Texts = new("ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantBrowser.FakeChatTexts", typeof(FakeChatClient).Assembly);
    private static string Text(string key) => Texts.GetString(key, System.Globalization.CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException("A fake provider fixture resource is missing.");
    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        metrics.Title();
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, Text("Assistant.Fake.Title")))
        { Usage = new UsageDetails { InputTokenCount = 1, OutputTokenCount = 1, TotalTokenCount = 2 } });
    }
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        metrics.Call();
        var input = messages.ToArray();
        var scenario = input.LastOrDefault(row => row.Role == ChatRole.User)?.Text ?? string.Empty;
        var toolResults = input.SelectMany(row => row.Contents).OfType<FunctionResultContent>()
            .Where(row => row.CallId != AssistantActionTools.PreloadCallId).ToArray();
        // La hora del pedido va en otro texto del mismo mensaje (0064 §9): la propuesta se lee solo de lo que escribió la persona.
        var written = input.LastOrDefault(row => row.Role == ChatRole.User)?.Contents.OfType<TextContent>().FirstOrDefault()?.Text ?? string.Empty;
        if (ActionScenario(written) is { } proposal)
        {
            if (toolResults.Length == 0)
            {
                var propose = options?.Tools?.SingleOrDefault(row => row.Name == AssistantActionTools.ProposeKey)
                    ?? throw new InvalidOperationException("The scripted action scenario requires the offered actions.");
                metrics.Tool();
                yield return new ChatResponseUpdate
                {
                    Role = ChatRole.Assistant,
                    Contents = [new FunctionCallContent(ProposeCallId, propose.Name, proposal)],
                };
                yield return new ChatResponseUpdate { Contents = [new UsageContent(new UsageDetails { InputTokenCount = 3, OutputTokenCount = 2, TotalTokenCount = 5 })], FinishReason = ChatFinishReason.ToolCalls };
                yield break;
            }
            await Task.Delay(TimeSpan.FromMilliseconds(200), clock, cancellationToken);
            yield return new ChatResponseUpdate(ChatRole.Assistant,
                Text(toolResults.Any(row => row.CallId == ProposeCallId && IsAccepted(row.Result)) ? "Assistant.Fake.Proposed" : "Assistant.Fake.NotProposed"));
            yield return new ChatResponseUpdate { Contents = [new UsageContent(new UsageDetails { InputTokenCount = 1, OutputTokenCount = 1, TotalTokenCount = 2 })], FinishReason = ChatFinishReason.Stop };
            yield break;
        }
        if (scenario.Contains("[E2E:tool]", StringComparison.Ordinal) && toolResults.Length == 0)
        {
            var tool = options?.Tools?.SingleOrDefault(row => row.Name == "org_users")
                ?? throw new InvalidOperationException("The scripted users scenario requires its declared tool.");
            metrics.Tool();
            yield return new ChatResponseUpdate
            {
                Role = ChatRole.Assistant,
                Contents = [new FunctionCallContent("e2e-read-only", tool.Name, new Dictionary<string, object?> { ["page"] = 1, ["pageSize"] = 20 })],
            };
            yield return new ChatResponseUpdate { Contents = [new UsageContent(new UsageDetails { InputTokenCount = 3, OutputTokenCount = 2, TotalTokenCount = 5 })], FinishReason = ChatFinishReason.ToolCalls };
            yield break;
        }
        yield return new ChatResponseUpdate(ChatRole.Assistant, Text("Assistant.Fake.Prefix"));
        await Task.Delay(scenario.Contains("[E2E:slow]", StringComparison.Ordinal) ? TimeSpan.FromSeconds(3) : TimeSpan.FromMilliseconds(200), clock, cancellationToken);
        if (scenario.Contains("[E2E:error]", StringComparison.Ordinal)) throw new HttpRequestException("Isolated provider failure.");
        yield return new ChatResponseUpdate(ChatRole.Assistant, Text("Assistant.Fake.Suffix"));
        yield return new ChatResponseUpdate { Contents = [new UsageContent(new UsageDetails { InputTokenCount = 1, OutputTokenCount = 1, TotalTokenCount = 2 })], FinishReason = ChatFinishReason.Stop };
    }
    internal const string ActionMarker = "[E2E:action]";
    private const string ProposeCallId = "e2e-propose";

    /// <summary>
    /// El escenario de una acción (I1 de las acciones): <c>[E2E:action]</c> seguido de la propuesta como la escribe el modelo,
    /// <c>{"action": "users.invite@1", "target"?: "…", "values": [{"field": "…", "value": …}]}</c>. El modelo falso llama una
    /// vez a <c>propose_action</c> con eso y, con su resultado, contesta que la preparó o que no pudo: la tarjeta queda en el
    /// hilo. Lo usan el recorrido y el E2E, con los nombres de su entorno. Sin el marcador, null; mal escrito, un error.
    /// </summary>
    internal static Dictionary<string, object?>? ActionScenario(string text)
    {
        var start = text.IndexOf(ActionMarker, StringComparison.Ordinal);
        if (start < 0) return null;
        using var document = JsonDocument.Parse(text[(start + ActionMarker.Length)..]);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("action", out var action) || action.ValueKind != JsonValueKind.String
            || !root.TryGetProperty("values", out var values) || values.ValueKind != JsonValueKind.Array
            || root.EnumerateObject().Any(property => property.Name is not ("action" or "target" or "values"))
            || (root.TryGetProperty("target", out var named) && named.ValueKind != JsonValueKind.String))
            throw new InvalidOperationException("The scripted action scenario needs its action, its values and an optional target.");
        var arguments = new Dictionary<string, object?> { ["action"] = action.GetString(), ["values"] = values.Clone() };
        if (root.TryGetProperty("target", out var target)) arguments["target"] = target.GetString();
        return arguments;
    }

    // Dentro del turno llega el dato; si el turno se rehízo desde lo guardado, su forma serializada.
    private static bool IsAccepted(object? result) => result switch
    {
        AssistantActionToolData data => data.Outcome == AssistantActionToolData.Accepted,
        JsonElement { ValueKind: JsonValueKind.Object } json => json.TryGetProperty("outcome", out var outcome)
            && outcome.ValueKind == JsonValueKind.String && outcome.GetString() == AssistantActionToolData.Accepted,
        string json => json.Contains("\"outcome\":\"" + AssistantActionToolData.Accepted + "\"", StringComparison.Ordinal),
        _ => false,
    };

    /// <summary>
    /// La calificación de prueba del modelo falso (ADR 0068 §12): las acciones del núcleo, calificadas con el modelo del turno.
    /// Solo la instalan los tests y el recorrido; producción carga <c>qualified-actions.json</c>, vacía hasta calificarlas.
    /// </summary>
    internal static void QualifyActions(IServiceCollection services, string providerModelId) =>
        services.Replace(ServiceDescriptor.Singleton(new AssistantActionQualifications(
            OrganizationUserActionSource.Actions.Concat(RoleActionSource.Actions)
                .Select(action => new AssistantActionQualification(action.Id, action.Version, providerModelId, new DateOnly(2026, 11, 26),
                    new Money(0.01m, CurrencyCode.Create("USD").Value))).ToArray())));
    /// <summary>
    /// La calificación del modelo falso en el entorno aislado del recorrido y del E2E: con cada modelo del catálogo inicial,
    /// porque el preparador elige el modelo después, con la base sembrada.
    /// </summary>
    internal static void QualifyActionsWithInitialModels(IServiceCollection services) =>
        services.Replace(ServiceDescriptor.Singleton(provider => new AssistantActionQualifications(
            provider.GetRequiredService<IAssistantProviderCatalog>().InitialModels
                .SelectMany(model => OrganizationUserActionSource.Actions.Concat(RoleActionSource.Actions)
                    .Select(action => new AssistantActionQualification(action.Id, action.Version, model.ProviderModelId, new DateOnly(2026, 11, 26),
                        new Money(0.01m, CurrencyCode.Create("USD").Value))))
                .ToArray())));
    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType == typeof(IChatClient) && serviceKey is null ? this : null;
    public void Dispose() { }
}
