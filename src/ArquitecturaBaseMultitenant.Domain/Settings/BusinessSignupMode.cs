namespace ArquitecturaBaseMultitenant.Domain.Settings;

/// <summary>
/// Indica si el registro de empresas está abierto, requiere aprobación o está cerrado. Permite aplicar la
/// política global del alta de organizaciones.
/// </summary>
public enum BusinessSignupMode
{
    Open,
    RequiresApproval,
    Closed,
}
