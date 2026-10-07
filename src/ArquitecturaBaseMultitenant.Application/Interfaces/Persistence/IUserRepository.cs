using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Escrituras y relectura transaccional de la identidad global sin filtrar por tenant.</summary>
public interface IUserRepository
{
    Task<Result> RequestDeletionAsync(Guid userId, string reason, DateTime requestedAtUtc, int graceDays,
        CancellationToken cancellationToken);

    /// <summary>Baja iniciada por un operador: una cuenta suspendida la conserva suspendida con la fecha.</summary>
    Task<Result> RequestDeletionByOperatorAsync(Guid userId, Guid operatorId, string reason, DateTime requestedAtUtc,
        int graceDays, CancellationToken cancellationToken);
    Task<Result> CancelDeletionAsync(Guid userId, DateTime cancelledAtUtc, CancellationToken cancellationToken);
    Task<Result> CompleteDeletionAsync(Guid userId, DateTime deletedAtUtc, string deletedDisplayName,
        CancellationToken cancellationToken);
    /// <summary>Suspende una cuenta Active dentro de la UoW del llamador.</summary>
    Task<Result> SuspendAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Levanta la suspensión: vuelve a Active o, con una baja programada, a PendingDeletion.</summary>
    Task<Result<UserStatus>> ReactivateAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Marca o desmarca la cuenta como operadora; los permisos se guardan aparte, en la misma UoW.</summary>
    Task SetPlatformOperatorAsync(Guid userId, bool isOperator, CancellationToken cancellationToken);

    /// <summary>ADR 0042: marca la cuenta nueva como operadora, con la invitación pendiente hasta la fecha dada.</summary>
    Task InvitePlatformOperatorAsync(Guid userId, DateTime expiresAtUtc, CancellationToken cancellationToken);

    /// <summary>Renueva el vencimiento de una invitación de operador sin aceptar.</summary>
    Task<Result> RenewOperatorInvitationAsync(Guid userId, DateTime expiresAtUtc, CancellationToken cancellationToken);

    /// <summary>Acepta la invitación de operador con el primer ingreso, si todavía no venció.</summary>
    Task<Result> AcceptOperatorInvitationAsync(Guid userId, DateTime acceptedAtUtc, CancellationToken cancellationToken);

    /// <summary>Revoca la invitación: la identidad queda eliminada y anónima, fuera de Cuentas.</summary>
    Task<Result> DiscardOperatorInvitationAsync(Guid userId, DateTime discardedAtUtc, string deletedDisplayName,
        CancellationToken cancellationToken);

    Task<UserAccountRow> CreateAsync(string? displayName, string culture, string timeZoneId,
        CancellationToken cancellationToken);

    Task<UserAccountRow?> GetByIdAsync(Guid userId, CancellationToken cancellationToken);

    Task SetPrimaryEmailAsync(Guid userId, Email email, CancellationToken cancellationToken);
    Task SetPrimaryContactAsync(Guid userId, Email? email, PhoneNumber? phoneNumber, CancellationToken cancellationToken);

    /// <summary>
    /// Deja «Tus datos» para guardar con la versión del perfil que leyó la pantalla y devuelve la nueva. Es propia del
    /// perfil, no el xmin de la cuenta, y vale si la transacción hace commit (docs/rules/concurrencia.md).
    /// </summary>
    Task<Result<uint>> UpdateProfileAsync(Guid userId, string? displayName, string culture, string timeZoneId,
        uint expectedVersion, CancellationToken cancellationToken);

    /// <summary>Importa partes Google ya normalizadas bajo el lock de la cuenta y la UoW del llamador.</summary>
    Task FillMissingProfileNamesAsync(Guid userId, string? firstName, string? lastName, CancellationToken cancellationToken);

    Task RememberBusinessTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken);
}
