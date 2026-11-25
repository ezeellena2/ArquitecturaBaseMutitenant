using ArquitecturaBaseMultitenant.Domain.Results;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;

public interface IWhatsAppMessageRetentionService
{
    /// <summary>La copia global de la entrada (90 días fijos desde que llegó) y las IP vencidas del cupo: un lote.</summary>
    Task<Result<int>> ClearExpiredTextsAsync(CancellationToken ct);

    /// <summary>Un lote del contenido vencido de los chats de la organización del alcance, con su plazo (272).</summary>
    Task<Result<int>> ClearExpiredOrganizationChatsAsync(CancellationToken ct);

    /// <summary>Un lote del contenido vencido de los chats del número de Plataforma, con su plazo (272).</summary>
    Task<Result<int>> ClearExpiredPlatformChatsAsync(CancellationToken ct);
}
