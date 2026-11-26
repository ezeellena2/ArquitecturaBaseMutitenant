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

    /// <summary>
    /// Lo armado como dato (ADR 0068, «Consecuencias»): los campos de una acción con <c>FieldsFromTarget</c>, que salen de la
    /// definición de su destino (las opciones de un catálogo), con su tipo y sus límites. Sin destino que resuelva, un error.
    /// </summary>
    Task<Result<IReadOnlyList<AssistantActionField>>> ReadTargetFieldsAsync(AssistantActionDefinition action, string target, CancellationToken cancellationToken);

    /// <summary>
    /// Vuelve a resolver una propuesta guardada (ADR 0068 §9): cada id guardado, el destino y los <c>Record</c>, con los permisos
    /// de ahora y en su empresa. Devuelve el destino como está ahora y cada valor como lo dibuja la pantalla, con su nombre.
    /// Lo que ya no resuelve es <c>NotFound</c>, y lo que quien pide ya no puede dar, <c>Forbidden</c>.
    /// </summary>
    Task<Result<AssistantActionReview>> ReviewAsync(AssistantActionResolution resolution, CancellationToken cancellationToken);

    /// <summary>Arma el pedido de la pantalla y corre su validador y las <c>Policy</c> de la ruta, sin guardar.</summary>
    Task<Result> ValidateAsync(AssistantActionResolution resolution, CancellationToken cancellationToken);
}
