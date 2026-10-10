namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;

/// <summary>Claves write-only, con un propósito global o ligado al alcance activo de la organización.</summary>
public interface IAssistantKeyProtector
{
    byte[] ProtectPlatform(string key);
    string UnprotectPlatform(byte[] ciphertext);
    byte[] ProtectOrganization(string key);
    string UnprotectOrganization(byte[] ciphertext);
}
