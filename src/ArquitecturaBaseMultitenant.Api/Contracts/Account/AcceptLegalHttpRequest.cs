using ArquitecturaBaseMultitenant.Application.Models.Legal;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Account;

/// <summary>
/// Recibe por HTTP la lista de documentos y versiones que la persona acepta. El controller la transforma en
/// la solicitud del servicio de aceptación legal.
/// </summary>
public sealed record AcceptLegalHttpRequest(IReadOnlyList<LegalAcceptanceItem>? Documents);
