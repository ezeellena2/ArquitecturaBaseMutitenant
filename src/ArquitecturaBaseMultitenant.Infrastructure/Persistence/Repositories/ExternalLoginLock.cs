using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

internal sealed class ExternalLoginLock(ApplicationDbContext context) : IExternalLoginLock
{
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
