namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantLinkParametersResponse(Guid? UserId, Guid? RoleId, Guid? CompanyId, Guid? TenantId, Guid? RecoveryId);
