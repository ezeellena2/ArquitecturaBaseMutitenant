using ArquitecturaBaseMultitenant.Application.Common.Pagination;

namespace ArquitecturaBaseMultitenant.Application.Models.AssistantActions;

/// <summary>
/// Los topes de las acciones que comparten el núcleo y las fuentes de cada área (ADR 0068 §12), cada uno en una sola
/// constante: cambiarlo cambia a la vez <c>read_action</c>, el esquema de <c>propose_action</c> y cada <c>*ActionSource</c>.
/// </summary>
public static class AssistantActionLimits
{
    /// <summary>Las opciones de un <c>Choice</c> o un <c>Choices</c> que trae una página de <c>read_action</c>.</summary>
    public const int ChoicesPerPage = 20;

    /// <summary>El largo del texto con que el modelo nombra el destino, una opción o un registro.</summary>
    public const int TextLength = 200;

    /// <summary>
    /// Cuántos parecidos lee una fuente para resolver un texto: la página más grande del lector, buscada con su
    /// <c>ApplySearch</c>, así el igual casi siempre está entre ellos.
    /// </summary>
    public static int Candidates => PagedRequest.MaxPageSize;
}
