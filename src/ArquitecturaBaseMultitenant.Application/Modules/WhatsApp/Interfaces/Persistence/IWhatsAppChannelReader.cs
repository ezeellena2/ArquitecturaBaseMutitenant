using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;

/// <summary>Proyecciones de los números: los vigentes de la organización del claim, el de Plataforma y los datos técnicos para enviar.</summary>
public interface IWhatsAppChannelReader
{
    /// <summary>Números vigentes de la organización, con el contador del mes en la zona de cada número.</summary>
    Task<PagedResult<WhatsAppNumberResponse>> ListOrganizationAsync(WhatsAppNumberListRequest request, DateTime nowUtc, CancellationToken ct);
    Task<WhatsAppNumberResponse?> FindOrganizationAsync(Guid id, DateTime nowUtc, CancellationToken ct);
    /// <summary>Cuántos números vigentes tiene la organización; con estado, solo los de ese estado.</summary>
    Task<int> CountOrganizationAsync(WhatsAppNumberStatus? status, CancellationToken ct);
    /// <summary>Los primeros números sin medio de pago, del más viejo al más nuevo.</summary>
    Task<IReadOnlyList<WhatsAppNumberPendingRow>> ListPaymentMissingAsync(int limit, CancellationToken ct);
    Task<WhatsAppPlatformNumberRow?> FindPlatformAsync(DateTime nowUtc, CancellationToken ct);
    /// <summary>El número vigente de Plataforma, sin credenciales, para el estado en memoria del canal.</summary>
    Task<WhatsAppPlatformChannelRow?> FindPlatformStateAsync(CancellationToken ct);
    Task<bool> IsActiveAsync(string phoneNumberId, CancellationToken ct);
    /// <summary>Las organizaciones con algún número vigente: las que pueden tener chats esperando.</summary>
    Task<IReadOnlyList<Guid>> ListOrganizationIdsAsync(CancellationToken ct);
    /// <summary>El número que recibió una entrada (por canal o, en filas anteriores, por el identificador de Meta), en cualquier estado.</summary>
    Task<WhatsAppInboundChannel?> FindInboundAsync(Guid? channelId, string phoneNumberId, CancellationToken ct);
    /// <summary>Credenciales cifradas para enviar: por canal o, en pedidos viejos, por el identificador de Meta.</summary>
    Task<WhatsAppSendingChannelRow?> FindSendingAsync(Guid? channelId, string phoneNumberId, CancellationToken ct);
}

public sealed record WhatsAppNumberPendingRow(Guid Id, string DisplayNumber, DateTime SinceUtc);

public sealed record WhatsAppPlatformNumberRow(WhatsAppNumberResponse Number, bool ConsultationsEnabled, int SystemMessagesThisMonth);

public sealed record WhatsAppPlatformChannelRow(Guid Id, string PhoneNumberId, string? DisplayNumber, WhatsAppNumberStatus Status,
    bool ConsultationsEnabled);

public sealed record WhatsAppSendingChannelRow(Guid Id, string PhoneNumberId, string WabaId, Guid? OrganizationId, WhatsAppNumberStatus Status, byte[] EncryptedToken)
{
    public override string ToString() => nameof(WhatsAppSendingChannelRow);
}
