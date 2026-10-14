using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;

/// <summary>La sincronización periódica del estado de las plantillas con Meta, sin entrar a ninguna organización.</summary>
public interface IWhatsAppTemplateSyncService
{
    Task<Result> SyncAsync(CancellationToken cancellationToken);
}
