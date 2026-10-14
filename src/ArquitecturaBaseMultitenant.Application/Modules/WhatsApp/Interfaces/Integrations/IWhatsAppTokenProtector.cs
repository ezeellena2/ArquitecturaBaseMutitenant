namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;

/// <summary>Cifra el token y el PIN de un número con un propósito ligado a ese número. Nunca se registran ni salen en un DTO.</summary>
public interface IWhatsAppTokenProtector
{
    byte[] Protect(Guid channelId, string secret);
    string Unprotect(Guid channelId, byte[] ciphertext);
    /// <summary>Como Unprotect, pero null si las claves de esta instalación no lo leen (un número que no se puede usar se saltea).</summary>
    string? TryUnprotect(Guid channelId, byte[] ciphertext);
}
