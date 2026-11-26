using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.AssistantActions;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>
/// Puerto de las acciones (ADR 0068 §5). Cada área, y cada módulo, declara sus acciones con una implementación que vive con
/// la ruta que guarda, de sufijo <c>ActionSource</c>. Solo lee: lista opciones, resuelve cada texto a un id permitido (con
/// RLS, los permisos y lo que quien pide puede dar) y valida armando el pedido de la pantalla, sin guardar nada.
/// </summary>
public interface IAssistantActionSource
{
    /// <summary>Las acciones del área, estáticas: no dependen de quién pide ni de la organización.</summary>
    IReadOnlyList<AssistantActionDefinition> Definitions { get; }

    /// <summary>Las opciones de un <c>Choice</c> o un <c>Choices</c>, de a 20 y solo lo que quien pide puede dar.</summary>
    Task<Result<PagedResult<AssistantActionChoiceRow>>> ListChoicesAsync(AssistantActionChoiceRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Resuelve el destino y cada valor a un id permitido. Sin coincidencia es un error con la ruta del campo; con varias,
    /// una duda. Sobre datos públicos o compartidos filtra además por la organización del claim.
    /// </summary>
    Task<Result<AssistantActionResolution>> ResolveAsync(AssistantActionDraft draft, CancellationToken cancellationToken);

    /// <summary>Arma el pedido de la pantalla y corre su validador y las <c>Policy</c> de la ruta, sin guardar.</summary>
    Task<Result> ValidateAsync(AssistantActionResolution resolution, CancellationToken cancellationToken);
}
