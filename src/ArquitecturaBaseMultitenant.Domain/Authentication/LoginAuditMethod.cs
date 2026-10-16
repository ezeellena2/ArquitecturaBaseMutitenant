namespace ArquitecturaBaseMultitenant.Domain.Authentication;

/// <summary>
/// Identifica el mecanismo usado en un intento de ingreso. La auditoría conserva este dato para distinguir
/// código, Google y los mecanismos de WhatsApp. El ingreso de un operador se registra al completar el segundo
/// factor (E9), con el autenticador o con un código de recuperación.
/// </summary>
public enum LoginAuditMethod
{
    Code,
    Google,
    WhatsAppCode,
    WhatsAppLink,
    Authenticator,
    RecoveryCode,
}
