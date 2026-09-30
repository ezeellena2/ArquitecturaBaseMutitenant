using ArquitecturaBaseMultitenant.Application.Models.Legal;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Account;

public sealed record AcceptLegalHttpRequest(IReadOnlyList<LegalAcceptanceItem>? Documents);
