using System.Security.Cryptography;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.AspNetCore.DataProtection;

namespace ArquitecturaBaseMultitenant.Api.Authentication;

/// <summary>Transporta el comprobante OAuth sin exponerlo en una URL ni abrir una sesión.</summary>
public sealed class PendingDeletionCookie(IDataProtectionProvider protection, TimeProvider timeProvider)
{
    private const string Name = "MtPendingDeletion";
    private readonly IDataProtector protector = protection.CreateProtector("AccountDeletion.Pending.v1");
    private static CookieOptions Options => new()
    {
        HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax,
        Path = "/api/auth/deletion", MaxAge = TimeSpan.FromMinutes(5),
    };

    public void Write(HttpContext context, Error error)
    {
        var metadata = error.Metadata ?? throw new InvalidOperationException("Pending deletion metadata is required.");
        var state = new PendingDeletionState((DateTime)metadata["scheduledForUtc"]!, (string)metadata["cancelTicket"]!,
            (string)metadata["timeZoneId"]!, (string)metadata["returnUrl"]!, (DateTime)metadata["cancelTicketExpiresAtUtc"]!);
        context.Response.Cookies.Append(Name, protector.Protect(JsonSerializer.Serialize(state)), Options);
    }

    public PendingDeletionState? Read(HttpContext context)
    {
        if (!context.Request.Cookies.TryGetValue(Name, out var value)) return null;
        try
        {
            var state = JsonSerializer.Deserialize<PendingDeletionState>(protector.Unprotect(value));
            return state is not null && state.ExpiresAtUtc > timeProvider.GetUtcNow().UtcDateTime ? state : null;
        }
        catch (CryptographicException) { return null; }
        catch (JsonException) { return null; }
    }

    public void Clear(HttpContext context) => context.Response.Cookies.Delete(Name, Options);
}
