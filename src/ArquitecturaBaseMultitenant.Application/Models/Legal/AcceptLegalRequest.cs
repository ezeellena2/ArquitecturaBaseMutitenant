namespace ArquitecturaBaseMultitenant.Application.Models.Legal;

/// <summary>
/// Identifica el documento y la versión exacta que una persona acepta. El servicio los compara con la versión
/// vigente antes de guardar la prueba de aceptación.
/// </summary>
public sealed record LegalAcceptanceItem(Guid Id, int Version);
/// <summary>Agrupa las aceptaciones solicitadas para validarlas y guardarlas en una sola operación.</summary>
public sealed record AcceptLegalRequest(IReadOnlyList<LegalAcceptanceItem>? Documents);
