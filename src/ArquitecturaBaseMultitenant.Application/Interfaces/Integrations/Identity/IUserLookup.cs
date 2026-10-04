using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;

/// <summary>Busca globalmente un método de ingreso por tipo y valor normalizado, y el nombre visible de una cuenta.</summary>
public interface IUserLookup
{
    Task<IReadOnlyList<Guid>> FindVerifiedUsersByEmailAsync(Email email, CancellationToken cancellationToken);

    Task<Guid?> FindVerifiedUserIdAsync(LoginMethodType type, string value, CancellationToken cancellationToken);

    Task<LoginMethodLookup?> FindMethodAsync(LoginMethodType type, string value,
        CancellationToken cancellationToken);

    Task<string?> FindDisplayNameAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed record LoginMethodLookup(Guid MethodId, Guid UserId, DateTime? VerifiedAtUtc);
