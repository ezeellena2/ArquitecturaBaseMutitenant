using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Models.Identity;

/// <summary>
/// Datos globales de la cuenta sin exponer ApplicationUser fuera de Infrastructure. OperatorInvitationExpiresAtUtc es
/// el vencimiento de una invitación de operador sin aceptar (ADR 0042).
/// </summary>
public sealed record UserAccountRow(Guid Id, string? DisplayName, string Culture, string TimeZoneId,
    UserStatus Status, bool IsPlatformOperator, Email? PrimaryEmail, Guid? LastBusinessTenantId,
    uint Version = 0, DateTime? DeletionScheduledForUtc = null, DateTime? OperatorInvitationExpiresAtUtc = null)
{
    /// <summary>Invitación de operador sin aceptar, vigente o vencida: solo se reenvía o se revoca.</summary>
    public bool HasPendingOperatorInvitation => Status == UserStatus.Active && OperatorInvitationExpiresAtUtc is not null;
}
