using System.Security.Cryptography;
using System.Text;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.DataProtection;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;

internal sealed class AssistantKeyProtector(IDataProtectionProvider provider, ITenantContext tenant, ICurrentUser user) : IAssistantKeyProtector
{
    internal const string PlatformPurpose = "ArquitecturaBaseMultitenant.Assistant.PlatformKey.v1";
    internal const string OrganizationPurpose = "ArquitecturaBaseMultitenant.Assistant.OrganizationKey.v1";
    public byte[] ProtectPlatform(string key)
    {
        if (user.Access != Access.Platform || user.UserId is null) throw new InvalidOperationException("Platform key writes require an operator.");
        return Protect(key, provider.CreateProtector(PlatformPurpose));
    }
    public string UnprotectPlatform(byte[] ciphertext)
    {
        if (user.UserId is null || user.Access is null) throw new InvalidOperationException("An execution identity is required.");
        return Unprotect(ciphertext, provider.CreateProtector(PlatformPurpose));
    }
    public bool CanUnprotectPlatform(byte[] ciphertext)
    {
        if (user.UserId is null || user.Access is null) throw new InvalidOperationException("An execution identity is required.");
        return CanUnprotect(ciphertext, provider.CreateProtector(PlatformPurpose));
    }
    public bool CanUnprotectOrganization(byte[] ciphertext) => CanUnprotect(ciphertext, Organization());
    public byte[] ProtectOrganization(string key) => Protect(key, Organization());
    public string UnprotectOrganization(byte[] ciphertext) => Unprotect(ciphertext, Organization());
    private IDataProtector Organization()
    {
        if (user.Access != Access.Business || user.UserId is null) throw new InvalidOperationException("Own keys require a business execution identity.");
        return provider.CreateProtector(OrganizationPurpose, tenant.RequiredTenantId.ToString("N"));
    }
    private static byte[] Protect(string key, IDataProtector protector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var bytes = Encoding.UTF8.GetBytes(key);
        try { return protector.Protect(bytes); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    /// <summary>Prueba el descifrado sin conservar la clave (0064 §7); solo una clave que el anillo no abre devuelve false.</summary>
    private static bool CanUnprotect(byte[] ciphertext, IDataProtector protector)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        byte[] bytes;
        try { bytes = protector.Unprotect(ciphertext); }
        catch (CryptographicException) { return false; }
        CryptographicOperations.ZeroMemory(bytes);
        return true;
    }
    private static string Unprotect(byte[] cipher, IDataProtector protector)
    {
        var bytes = protector.Unprotect(cipher);
        try { return Encoding.UTF8.GetString(bytes); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
}
