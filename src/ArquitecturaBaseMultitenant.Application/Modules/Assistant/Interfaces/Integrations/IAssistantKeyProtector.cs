namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;

/// <summary>Claves write-only, con un propósito global o ligado al alcance activo de la organización.</summary>
public interface IAssistantKeyProtector
{
    byte[] ProtectPlatform(string key);
    string UnprotectPlatform(byte[] ciphertext);
    byte[] ProtectOrganization(string key);
    string UnprotectOrganization(byte[] ciphertext);
    /// <summary>Si la clave guardada se puede descifrar acá, sin devolverla (0064 §7: la disponibilidad prueba el descifrado).</summary>
    bool CanUnprotectPlatform(byte[] ciphertext);
    bool CanUnprotectOrganization(byte[] ciphertext);
}
