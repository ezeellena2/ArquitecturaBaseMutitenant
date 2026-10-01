namespace ArquitecturaBaseMultitenant.Domain.Authentication;

/// <summary>
/// Identifica el mecanismo usado en un intento de ingreso. La auditoría conserva este dato para distinguir
/// código, Google y los mecanismos de WhatsApp.
/// </summary>
public enum LoginAuditMethod
{
    Code,
    Google,
    WhatsAppCode,
    WhatsAppLink,
}
