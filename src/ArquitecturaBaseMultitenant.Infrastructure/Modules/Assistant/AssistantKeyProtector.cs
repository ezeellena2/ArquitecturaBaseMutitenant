using System.Security.Cryptography;
using System.Text;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.DataProtection;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;

internal sealed class AssistantKeyProtector(IDataProtectionProvider provider, ITenantContext tenant, ICurrentUser user) : IAssistantKeyProtector
{
    public byte[] ProtectPlatform(string key)
    {
        if (user.Access != Access.Platform || user.UserId is null) throw new InvalidOperationException("Platform key writes require an operator.");
        return Protect(key, provider.CreateProtector("ArquitecturaBaseMultitenant.Assistant.PlatformKey.v1"));
    }
    public string UnprotectPlatform(byte[] ciphertext)
    {
        if (user.UserId is null || user.Access is null) throw new InvalidOperationException("An execution identity is required.");
        return Unprotect(ciphertext, provider.CreateProtector("ArquitecturaBaseMultitenant.Assistant.PlatformKey.v1"));
    }
    public byte[] ProtectOrganization(string key) => Protect(key, Organization());
    public string UnprotectOrganization(byte[] ciphertext) => Unprotect(ciphertext, Organization());
    private IDataProtector Organization()
    {
        if (user.Access != Access.Business || user.UserId is null) throw new InvalidOperationException("Own keys require a business execution identity.");
        return provider.CreateProtector("ArquitecturaBaseMultitenant.Assistant.OrganizationKey.v1", tenant.RequiredTenantId.ToString("N"));
    }
    private static byte[] Protect(string key, IDataProtector protector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var bytes = Encoding.UTF8.GetBytes(key);
        try { return protector.Protect(bytes); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    private static string Unprotect(byte[] cipher, IDataProtector protector)
    {
        var bytes = protector.Unprotect(cipher);
        try { return Encoding.UTF8.GetString(bytes); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
}
