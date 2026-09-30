using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Escrituras y relectura transaccional de la identidad global sin filtrar por tenant.</summary>
public interface IUserRepository
{
    Task<Result> RequestDeletionAsync(Guid userId, string reason, DateTime requestedAtUtc, int graceDays,
        CancellationToken cancellationToken);
    Task<Result> CancelDeletionAsync(Guid userId, DateTime cancelledAtUtc, CancellationToken cancellationToken);
    Task<Result> CompleteDeletionAsync(Guid userId, DateTime deletedAtUtc, string deletedDisplayName,
        CancellationToken cancellationToken);
    Task<UserAccountRow> CreateAsync(string? displayName, string culture, string timeZoneId,
        CancellationToken cancellationToken);

    Task<UserAccountRow?> GetByIdAsync(Guid userId, CancellationToken cancellationToken);

    Task SetPrimaryEmailAsync(Guid userId, Email email, CancellationToken cancellationToken);
    Task SetPrimaryContactAsync(Guid userId, Email? email, PhoneNumber? phoneNumber, CancellationToken cancellationToken);

    Task UpdateProfileAsync(Guid userId, string? displayName, string culture, string timeZoneId,
        uint expectedVersion, CancellationToken cancellationToken);

    Task RememberBusinessTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken);
}
