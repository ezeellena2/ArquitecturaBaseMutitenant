using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Serializa callbacks del mismo Google subject o correo verificado.</summary>
public interface IExternalLoginLock
{
    Task LockEmailAsync(Email email, CancellationToken cancellationToken);

    Task AcquireAsync(string providerSubject, Email? email, CancellationToken cancellationToken);
}
