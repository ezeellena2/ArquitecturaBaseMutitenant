using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Models.Identity;

/// <summary>Método de ingreso visible para los casos de uso de una cuenta.</summary>
public sealed record LoginMethodRow(Guid Id, LoginMethodType Type, string Value,
    bool IsPrimary, DateTime? VerifiedAtUtc, Guid? ManagedByTenantId, Email? ContactEmail = null,
    Guid? InvitationOriginTenantId = null)
{
    public bool IsPersonalBackup => VerifiedAtUtc is not null
        && ManagedByTenantId is null && InvitationOriginTenantId is null;
}
