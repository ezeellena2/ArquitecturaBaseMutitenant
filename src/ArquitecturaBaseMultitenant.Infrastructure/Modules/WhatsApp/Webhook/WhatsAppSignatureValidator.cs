using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using Microsoft.Extensions.Options;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Webhook;
internal sealed class WhatsAppSignatureValidator(IOptions<WhatsAppOptions> options) : IWhatsAppSignatureValidator
{
    public bool IsValidSignature(ReadOnlySpan<byte> body, string? signature)
    {
        const string prefix = "sha256=";
        if (string.IsNullOrEmpty(options.Value.AppSecret) || signature is null || !signature.StartsWith(prefix, StringComparison.Ordinal)) return false;
        Span<byte> received = stackalloc byte[HMACSHA256.HashSizeInBytes];
        if (Convert.FromHexString(signature.AsSpan(prefix.Length), received, out var read, out var written) != OperationStatus.Done
            || read != signature.Length - prefix.Length || written != received.Length) return false;
        Span<byte> expected = stackalloc byte[HMACSHA256.HashSizeInBytes];
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.Value.AppSecret), body, expected);
        return CryptographicOperations.FixedTimeEquals(expected, received);
    }
    public bool IsValidVerifyToken(string? token) => !string.IsNullOrEmpty(options.Value.VerifyToken) && !string.IsNullOrEmpty(token)
        && CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(options.Value.VerifyToken)), SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
