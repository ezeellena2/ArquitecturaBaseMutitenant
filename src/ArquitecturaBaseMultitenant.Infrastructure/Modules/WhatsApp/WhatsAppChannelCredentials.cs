using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;

/// <summary>El número de Meta y el token descifrado con que sale un envío (decisión 7); un número desconectado no envía.</summary>
internal sealed class WhatsAppChannelCredentials(IWhatsAppChannelReader reader, IWhatsAppTokenProtector protector)
{
    public async Task<WhatsAppChannelCredential?> FindAsync(Guid? channelId, string phoneNumberId, CancellationToken ct)
    {
        var row = await reader.FindSendingAsync(channelId, phoneNumberId, ct);
        if (row is null || row.Status == WhatsAppNumberStatus.Disconnected || row.EncryptedToken.Length == 0) return null;
        return new WhatsAppChannelCredential(row.PhoneNumberId, protector.Unprotect(row.Id, row.EncryptedToken));
    }
}

internal sealed record WhatsAppChannelCredential(string PhoneNumberId, string? Token)
{
    public override string ToString() => nameof(WhatsAppChannelCredential);
}
