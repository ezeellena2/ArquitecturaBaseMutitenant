using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.Models.Identity;

/// <summary>Método de ingreso visible para los casos de uso de una cuenta.</summary>
public sealed record LoginMethodRow(Guid Id, LoginMethodType Type, string Value,
    bool IsPrimary, DateTime? VerifiedAtUtc, Guid? ManagedByTenantId);
