using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using Microsoft.AspNetCore.DataProtection;

namespace ArquitecturaBaseMultitenant.Infrastructure.Security;

/// <summary>Cifra el payload del outbox con el anillo persistido de Data Protection.</summary>
internal sealed class PayloadProtector(IDataProtectionProvider provider) : IPayloadProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("ArquitecturaBaseMultitenant.Outbox.Payload.v1");

    public string Protect(string payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return _protector.Protect(payload);
    }

    public string Unprotect(string protectedPayload)
    {
        ArgumentNullException.ThrowIfNull(protectedPayload);
        return _protector.Unprotect(protectedPayload);
    }
}
