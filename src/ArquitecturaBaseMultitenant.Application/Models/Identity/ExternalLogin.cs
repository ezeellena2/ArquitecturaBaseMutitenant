namespace ArquitecturaBaseMultitenant.Application.Models.Identity;

/// <summary>Claims seleccionados del proveedor externo después del callback.</summary>
public sealed record ExternalLogin(
    string Provider,
    string ProviderKey,
    string? Email,
    bool EmailVerified,
    string? DisplayName);
