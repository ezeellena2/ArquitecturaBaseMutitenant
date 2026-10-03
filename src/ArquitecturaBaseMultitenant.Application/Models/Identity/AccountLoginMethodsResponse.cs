using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.Models.Identity;

/// <summary>Entrega métodos de ingreso y capacidades de gestión de la cuenta sin exponer los objetos de persistencia.</summary>
/// <remarks><c>SoleOwnerOrganizations</c>: organizaciones donde la cuenta es la única Dueña; mientras haya alguna, no puede pedir la baja.</remarks>
public sealed record AccountLoginMethodsResponse(IReadOnlyList<AccountLoginMethodResponse> Methods,
    bool CanLinkGoogle, bool NeedsPersonalLoginMethod, int AccountDeletionGraceDays,
    IReadOnlyList<string> SoleOwnerOrganizations)
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
