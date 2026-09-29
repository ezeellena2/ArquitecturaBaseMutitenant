using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Models.Auth;

/// <summary>El lado pedido y, en Business, la organización elegida si la selección es explícita.</summary>
public sealed record AccessSelectionRequest(Access Access, Guid? TenantId);
