namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;

/// <summary>Cifra el token y el PIN de un número con un propósito ligado a ese número. Nunca se registran ni salen en un DTO.</summary>
public interface IWhatsAppTokenProtector
{
    byte[] Protect(Guid channelId, string secret);
    string Unprotect(Guid channelId, byte[] ciphertext);
}
