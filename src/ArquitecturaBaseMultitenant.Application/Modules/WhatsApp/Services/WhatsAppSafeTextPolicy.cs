using ArquitecturaBaseMultitenant.Application.Common.Text;
using ArquitecturaBaseMultitenant.Domain.Common;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
/// <summary>Retención conservadora: ningún OTP, URL ni cadena con forma de credencial.</summary>
internal static class WhatsAppSafeTextPolicy
{
    internal static string? Retain(string? text)
    {
        var clean = TextNormalizer.Clean(text);
        if (clean is null || clean.Length > TextLimits.LongText || clean.Contains("://", StringComparison.Ordinal)
            || clean.Contains("www.", StringComparison.OrdinalIgnoreCase) || clean.Contains("token", StringComparison.OrdinalIgnoreCase)
            || clean.Contains("bearer", StringComparison.OrdinalIgnoreCase)) return null;
        var digits = 0; var word = 0;
        foreach (var character in clean)
        {
            if (char.IsDigit(character)) digits++;
            else if (!char.IsWhiteSpace(character) && character is not ('-' or '.')) digits = 0;
            word = char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '/' or '=' ? word + 1 : 0;
            // LoginCodeOptions admite desde cuatro dígitos: no depender de la longitud configurada hoy.
            if (digits >= 4 || word >= 32) return null;
        }
        return clean;
    }
}
