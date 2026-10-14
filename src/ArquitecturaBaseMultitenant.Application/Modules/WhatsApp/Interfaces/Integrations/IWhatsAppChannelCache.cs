namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;

/// <summary>El número de Plataforma que usan códigos, avisos e ingreso.</summary>
public interface IWhatsAppChannelCache
{
    /// <summary>Lo crea desde la configuración si hace falta y lo carga, una vez por proceso.</summary>
    Task LoadAsync(CancellationToken ct);
    /// <summary>Lo relee después de un cambio confirmado.</summary>
    Task RefreshAsync(CancellationToken ct);
}
