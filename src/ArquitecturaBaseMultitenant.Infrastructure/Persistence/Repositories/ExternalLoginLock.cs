using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

/// <summary>Serializa el vínculo de una cuenta con un subject de Google y, si existe, su correo verificado. Adquiere los locks antes de buscar o crear métodos de ingreso.</summary>
internal sealed class ExternalLoginLock(ApplicationDbContext context) : IExternalLoginLock
{
    public Task LockEmailAsync(Email email, CancellationToken cancellationToken) =>
        context.AcquireAdvisoryLocksAsync([AdvisoryLockKeys.ExternalGoogleEmail(email.Value)], cancellationToken);

    public Task AcquireAsync(string providerSubject, Email? email, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerSubject);
        var keys = new List<string> { AdvisoryLockKeys.ExternalGoogleSubject(providerSubject) };
        if (email is not null)
        {
            keys.Add(AdvisoryLockKeys.ExternalGoogleEmail(email.Value));
        }

        return context.AcquireAdvisoryLocksAsync(keys, cancellationToken);
    }
}
