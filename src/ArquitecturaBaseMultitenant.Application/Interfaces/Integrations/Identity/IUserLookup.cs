using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;

/// <summary>Resuelve una identidad global desde un método de ingreso verificado.</summary>
public interface IUserLookup
{
    Task<Guid?> FindVerifiedUserIdAsync(LoginMethodType type, string value, CancellationToken cancellationToken);
}
