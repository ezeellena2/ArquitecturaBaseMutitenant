using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;

/// <summary>Busca globalmente un método de ingreso por tipo y valor normalizado.</summary>
public interface IUserLookup
{
    Task<Guid?> FindVerifiedUserIdAsync(LoginMethodType type, string value, CancellationToken cancellationToken);

    Task<LoginMethodLookup?> FindMethodAsync(LoginMethodType type, string value,
        CancellationToken cancellationToken);
}

public sealed record LoginMethodLookup(Guid MethodId, Guid UserId, DateTime? VerifiedAtUtc);
