namespace ArquitecturaBaseMultitenant.Domain.Auditing;

public enum SecurityEventType
{
    LoginMethodChanged,
    AccountDeletionRequested,
    AccountDeletionCancelled,
    AccountDeleted,
    PlatformSettingsChanged,
}
