namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;

/// <summary>
/// Si una organización tiene prendido el módulo, para lo que corre sin sesión fuera de Application: la cola de salida no
/// manda una difusión de una organización con el módulo apagado. Lo responde <c>WhatsAppModulePolicy</c> con la caché de las
/// habilitaciones, así no suma una consulta por mensaje.
/// </summary>
public interface IWhatsAppModuleState
{
    Task<bool> IsEnabledAsync(Guid organizationId, CancellationToken ct);
}
