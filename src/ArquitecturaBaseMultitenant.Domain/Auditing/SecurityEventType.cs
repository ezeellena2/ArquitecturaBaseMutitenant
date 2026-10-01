namespace ArquitecturaBaseMultitenant.Domain.Auditing;

/// <summary>
/// Identifica el cambio sensible registrado en un evento de seguridad. Permite distinguir cambios de
/// ingreso, bajas de cuenta y acciones administrativas de plataforma.
/// </summary>
public enum SecurityEventType
{
    LoginMethodChanged,
    AccountDeletionRequested,
    AccountDeletionCancelled,
    AccountDeleted,
    PlatformSettingsChanged,
    PlatformOperatorGranted,
}
