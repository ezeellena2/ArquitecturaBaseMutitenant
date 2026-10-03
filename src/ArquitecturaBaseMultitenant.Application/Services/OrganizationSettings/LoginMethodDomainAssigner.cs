using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationSettings;

/// <summary>Marca solo métodos cuyo correo coincide con un dominio verificado, sin crear un acceso.</summary>
internal sealed class LoginMethodDomainAssigner(ITenantDomainRepository domains)
{
    public async Task AssignIfVerifiedAsync(LoginMethod method, Email? email,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(method);
        if (email is null) return;
        var address = email.Value;
        var separator = address.LastIndexOf('@');
        if (separator < 1) return;
        var emailDomain = OrganizationEmailDomain.Create(address[(separator + 1)..]);
        if (emailDomain.IsFailure) return;
        var organizationId = await domains.FindVerifiedOrganizationIdAsync(emailDomain.Value, cancellationToken);
        if (organizationId is { } id) method.AssignManagedByOrganization(id);
    }
}
