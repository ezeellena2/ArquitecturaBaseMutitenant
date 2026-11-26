using System.Net.Http.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Models.AssistantActions;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantActions;

/// <summary>
/// El escenario de una acción para la batería genérica (ADR 0068, «Verificación»): la organización, quién pide con su sesión
/// de empresa, otra organización, los casos de la acción (válido, inválido, ambiguo, con instrucciones escondidas, con algo
/// de otra organización y, si la acción elige una empresa, con algo de otra empresa) y cómo guarda la pantalla el caso válido
/// con lo que muestra la propuesta. Quien lo usa libera <see cref="Client"/>.
/// </summary>
internal sealed record AssistantActionScenario(
    string Action,
    AccountJourney Requester,
    HttpClient Client,
    Guid TenantId,
    Guid OtherTenantId,
    AssistantActionScenarioCase Valid,
    AssistantActionScenarioCase Invalid,
    AssistantActionScenarioCase Ambiguous,
    AssistantActionScenarioCase Hidden,
    AssistantActionScenarioCase Foreign,
    AssistantActionSave SaveAsync)
{
    /// <summary>Algo de otra empresa de la misma organización; null si la acción no elige empresa.</summary>
    public AssistantActionScenarioCase? OtherCompany { get; init; }

    public IEnumerable<(string Name, AssistantActionScenarioCase Case)> Cases =>
        OtherCompany is { } otherCompany
            ? [("valid", Valid), ("invalid", Invalid), ("ambiguous", Ambiguous), ("hidden", Hidden), ("foreign", Foreign), ("otherCompany", otherCompany)]
            : [("valid", Valid), ("invalid", Invalid), ("ambiguous", Ambiguous), ("hidden", Hidden), ("foreign", Foreign)];
}

/// <summary>
/// El cuerpo HTTP del caso válido, como lo manda la pantalla con la propuesta cargada: lo que lee al abrir lo lee con la
/// sesión del escenario, y lo manda <paramref name="sender"/>, con <paramref name="idempotencyKey"/> si la ruta crea (una
/// nueva si es null). Un segundo envío con la misma propuesta es el de otra pestaña: lleva la versión que mostró la propuesta.
/// </summary>
internal delegate Task<HttpResponseMessage> AssistantActionSave(HttpClient sender, AssistantActionReview review, Guid? idempotencyKey,
    CancellationToken ct);

/// <summary>Los pedidos que arman los escenarios.</summary>
internal static class AssistantActionRequests
{
    /// <summary>Un <c>POST</c> con la clave del intento, como lo manda la pantalla.</summary>
    internal static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object body, Guid? idempotencyKey, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", (idempotencyKey ?? Guid.NewGuid()).ToString("D"));
        return await client.SendAsync(request, ct);
    }
}

/// <summary>Un caso: el destino y los valores como los escribe el modelo, y lo que se espera de la fuente.</summary>
internal sealed record AssistantActionScenarioCase(string? Target, IReadOnlyList<AssistantActionInput> Values)
{
    /// <summary>La ruta del campo que la fuente rechaza; null si lo acepta.</summary>
    public string? RejectedPath { get; init; }

    /// <summary>Si lo acepta, los campos que quedan con «Revisá».</summary>
    public IReadOnlyList<string> Doubtful { get; init; } = [];

    /// <summary>Si lo acepta, el nombre que la pantalla tiene que mostrar (el del destino o el de una opción), leído del servidor.</summary>
    public string? ShownLabel { get; init; }
}

/// <summary>Los escenarios de un área, uno por acción: la batería los junta.</summary>
internal interface IAssistantActionScenarioSource
{
    IReadOnlyList<string> Actions { get; }

    Task<AssistantActionScenario> PrepareAsync(ApiFactory factory, string action, CancellationToken ct);
}
