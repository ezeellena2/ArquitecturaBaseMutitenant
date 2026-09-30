namespace ArquitecturaBaseMultitenant.Application.Models.Legal;

public sealed record LegalAcceptanceItem(Guid Id, int Version);
public sealed record AcceptLegalRequest(IReadOnlyList<LegalAcceptanceItem>? Documents);
