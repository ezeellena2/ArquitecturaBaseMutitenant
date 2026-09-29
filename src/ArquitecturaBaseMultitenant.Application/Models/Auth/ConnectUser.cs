using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Models.Auth;

/// <summary>Cuenta y acceso ya resueltos para emitir el principal del token.</summary>
public sealed record ConnectUser(UserAccountRow Account, Access Access, Guid? TenantId, TenantKind? TenantKind);
