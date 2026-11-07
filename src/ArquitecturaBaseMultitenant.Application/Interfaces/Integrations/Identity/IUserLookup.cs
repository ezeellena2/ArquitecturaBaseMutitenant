using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;

/// <summary>Busca globalmente un método de ingreso por tipo y valor normalizado, y el nombre visible de una cuenta.</summary>
public interface IUserLookup
{
    Task<IReadOnlyList<Guid>> FindVerifiedUsersByEmailAsync(Email email, CancellationToken cancellationToken);

    Task<Guid?> FindVerifiedUserIdAsync(LoginMethodType type, string value, CancellationToken cancellationToken);

    /// <summary>
    /// <see cref="FindVerifiedUserIdAsync"/> para varios valores en una sola consulta: la cuenta de cada valor verificado; un
    /// valor sin cuenta no figura.
    /// </summary>
    Task<IReadOnlyDictionary<string, Guid>> FindVerifiedUserIdsAsync(LoginMethodType type, IReadOnlyCollection<string> values,
        CancellationToken cancellationToken);

    Task<LoginMethodLookup?> FindMethodAsync(LoginMethodType type, string value,
        CancellationToken cancellationToken);

    /// <summary>Todas las reservas no verificadas del tipo y valor exactos; no consulta ContactEmail.</summary>
    Task<IReadOnlyList<LoginMethodLookup>> FindPendingMethodsAsync(LoginMethodType type, string value,
        CancellationToken cancellationToken);

    Task<string?> FindDisplayNameAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed record LoginMethodLookup(Guid MethodId, Guid UserId, DateTime? VerifiedAtUtc);
