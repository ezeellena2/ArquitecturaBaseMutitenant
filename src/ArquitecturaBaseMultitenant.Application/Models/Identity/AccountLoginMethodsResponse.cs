using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.Models.Identity;

/// <summary>Entrega métodos de ingreso y capacidades de gestión de la cuenta sin exponer los objetos de persistencia.</summary>
public sealed record AccountLoginMethodsResponse(IReadOnlyList<AccountLoginMethodResponse> Methods,
    bool CanLinkGoogle, bool NeedsPersonalLoginMethod, int AccountDeletionGraceDays)
{
    public override string ToString() => nameof(AccountLoginMethodsResponse);
}

/// <summary>Describe disponibilidad y acciones de un método sin incluir su contacto completo en la representación de diagnóstico.</summary>
public sealed record AccountLoginMethodResponse(Guid Id, LoginMethodType Type, string? Value,
    bool IsPrimary, bool IsVerified, Guid? ManagedByTenantId, string? ManagedByOrganizationName,
    bool CanRemove, bool CanMakePrimary, string? BackupDestination)
{
    public override string ToString() => nameof(AccountLoginMethodResponse);
}
