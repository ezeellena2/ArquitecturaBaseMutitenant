namespace ArquitecturaBaseMultitenant.Application.Models.Legal;

public sealed record AccountDeletionWork(Guid UserId, Guid LeaseId, IReadOnlyList<AccountDeletionTenant> Tenants);
