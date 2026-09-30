namespace ArquitecturaBaseMultitenant.Application.Models.Legal;

/// <summary>Transporta el lease de una baja reclamada y sus organizaciones para procesar cada alcance por separado.</summary>
public sealed record AccountDeletionWork(Guid UserId, Guid LeaseId, IReadOnlyList<AccountDeletionTenant> Tenants);
