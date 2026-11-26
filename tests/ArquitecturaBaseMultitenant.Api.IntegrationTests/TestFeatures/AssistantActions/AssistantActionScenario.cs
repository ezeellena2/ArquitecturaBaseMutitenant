using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Models.AssistantActions;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantActions;

/// <summary>
/// El escenario de una acción para la batería genérica (ADR 0068, «Verificación»): la organización, quién pide con su sesión
/// de empresa, otra organización, los casos de la acción (válido, inválido, ambiguo, con instrucciones escondidas y con algo
/// de otra organización) y cómo guarda la pantalla el caso válido con lo que muestra la propuesta. Quien lo usa libera
/// <see cref="Client"/>.
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
    Func<AssistantActionReview, CancellationToken, Task<HttpResponseMessage>> SaveAsync)
{
    public IEnumerable<(string Name, AssistantActionScenarioCase Case)> Cases =>
        [("valid", Valid), ("invalid", Invalid), ("ambiguous", Ambiguous), ("hidden", Hidden), ("foreign", Foreign)];
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
