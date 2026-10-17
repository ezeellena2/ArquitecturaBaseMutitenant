using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;

/// <summary>Números conectados: los de la organización del claim o el de Plataforma. Exige la transacción del caso de uso.</summary>
public interface IWhatsAppChannelRepository
{
    /// <summary>Serializa el alta de un mismo número de Meta, venga de donde venga.</summary>
    Task LockNumberAsync(string phoneNumberId, CancellationToken ct);
    /// <summary>Un número de la organización activa, conectado o sin medio de pago; con versión, la exige.</summary>
    Task<WhatsAppChannel?> GetOrganizationAsync(Guid id, uint? expectedVersion, CancellationToken ct);
    /// <summary>El número vigente de Plataforma, bajo su lock; con versión, la exige.</summary>
    Task<WhatsAppChannel?> GetPlatformAsync(uint? expectedVersion, CancellationToken ct);
    /// <summary>El número vigente (no desconectado) con ese identificador de Meta, de quien sea.</summary>
    Task<WhatsAppChannel?> GetActiveAsync(string phoneNumberId, CancellationToken ct);
    /// <summary>Los números vigentes de una cuenta de Meta.</summary>
    Task<IReadOnlyList<WhatsAppChannel>> ListActiveByWabaAsync(string wabaId, CancellationToken ct);
    /// <summary>El número de una conversación, en cualquier estado: uno desconectado deja sus chats para leer (D7).</summary>
    Task<WhatsAppChannel?> GetAsync(Guid id, CancellationToken ct);
    Task<WhatsAppChannelUsage?> GetUsageAsync(Guid channelId, DateOnly month, CancellationToken ct);
    void Add(WhatsAppChannel channel);
    void Add(WhatsAppChannelUsage usage);
    void MarkUpdated(WhatsAppChannel channel);
}
