using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Models.Request;

public sealed record ExecutionActor(Guid OwnerUserId, Access Access, Guid? TenantId, string Culture, string TimeZoneId);
