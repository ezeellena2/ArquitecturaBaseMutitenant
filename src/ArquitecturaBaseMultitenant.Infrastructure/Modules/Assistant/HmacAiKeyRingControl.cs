using System.Security.Cryptography;
using System.Text;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;

/// <summary>
/// 0064 §11: el control de una clave del anillo de Data Protection es el sello autenticado (AES-256-CBC con HMAC-SHA256, el
/// algoritmo del anillo) de una etiqueta fija con esa clave, atado a su id, al nombre de la aplicación y a un propósito propio.
/// La primera copia lo escribe; las demás lo abren con su clave del mismo id, lo que recalcula su HMAC: otros bytes bajo ese id
/// u otro nombre de aplicación no lo abren. No revela la clave ni sirve para abrir otra cosa, y la clave nunca sale de Data
/// Protection. Como en <see cref="AssistantKeyProtector"/>, cada uso tiene su propósito.
/// </summary>
internal sealed class HmacAiKeyRingControl(IKeyManager keyManager, IDataProtectionProvider provider,
    IOptions<DataProtectionOptions> options) : IAiKeyRingControl
{
    internal const string Purpose = "ArquitecturaBaseMultitenant.Assistant.KeyRingControl.v1";
    private static readonly byte[] Label = "ArquitecturaBaseMultitenant assistant key ring control"u8.ToArray();
    private Dictionary<Guid, IAuthenticatedEncryptor>? encryptors;

    public AiKeyRingKeys ReadKeys()
    {
        // Un cifrado crea la primera clave si el anillo está vacío: la primera copia escribe su control en esta misma vuelta.
        _ = provider.CreateProtector(Purpose).Protect(Label);
        var readable = new Dictionary<Guid, IAuthenticatedEncryptor>();
        var unreadable = new List<Guid>();
        foreach (var key in keyManager.GetAllKeys())
        {
            if (Encryptor(key) is { } encryptor) readable[key.KeyId] = encryptor;
            else unreadable.Add(key.KeyId);
        }
        encryptors = readable;
        return new AiKeyRingKeys([.. readable.Keys.Order()], [.. unreadable.Distinct().Order()]);
    }

    public byte[] CreateControl(Guid keyId) => Required(keyId).Encrypt(new ArraySegment<byte>(Label), new ArraySegment<byte>(Context(keyId)));

    public bool Matches(Guid keyId, byte[] control)
    {
        ArgumentNullException.ThrowIfNull(control);
        byte[] opened;
        try { opened = Required(keyId).Decrypt(new ArraySegment<byte>(control), new ArraySegment<byte>(Context(keyId))); }
        catch (CryptographicException) { return false; }
        return CryptographicOperations.FixedTimeEquals(opened, Label);
    }

    private IAuthenticatedEncryptor Required(Guid keyId) =>
        (encryptors ?? throw new InvalidOperationException("Read the key ring first.")).TryGetValue(keyId, out var encryptor)
            ? encryptor : throw new InvalidOperationException("The key is not usable in this copy's ring.");

    /// <summary>Una clave que esta copia no puede abrir (otro certificado, un descriptor ilegible) no es de su anillo.</summary>
    private static IAuthenticatedEncryptor? Encryptor(IKey key)
    {
        try { return key.CreateEncryptor(); }
        catch (Exception exception) when (exception is CryptographicException or InvalidOperationException or FormatException
            or System.Xml.XmlException) { return null; }
    }

    private byte[] Context(Guid keyId) =>
        Encoding.UTF8.GetBytes(Purpose + "\n" + (options.Value.ApplicationDiscriminator ?? string.Empty) + "\n" + keyId.ToString("N"));
}
