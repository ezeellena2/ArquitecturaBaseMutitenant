using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Models.Identity;

/// <summary>Datos globales de la cuenta sin exponer ApplicationUser fuera de Infrastructure.</summary>
public sealed record UserAccountRow(Guid Id, string? DisplayName, string Culture, string TimeZoneId,
    UserStatus Status, bool IsPlatformOperator, Email? PrimaryEmail, Guid? LastBusinessTenantId);
