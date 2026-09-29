using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Escrituras y relectura transaccional de la identidad global sin filtrar por tenant.</summary>
public interface IUserRepository
{
    Task<UserAccountRow> CreateAsync(string? displayName, string culture, string timeZoneId,
        CancellationToken cancellationToken);

    Task<UserAccountRow?> GetByIdAsync(Guid userId, CancellationToken cancellationToken);

    Task SetPrimaryEmailAsync(Guid userId, Email email, CancellationToken cancellationToken);

    Task UpdateProfileAsync(Guid userId, string? displayName, string culture, string timeZoneId,
        CancellationToken cancellationToken);

    Task RememberBusinessTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken);
}
