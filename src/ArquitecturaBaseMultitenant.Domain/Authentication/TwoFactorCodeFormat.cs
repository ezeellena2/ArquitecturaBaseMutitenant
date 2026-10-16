namespace ArquitecturaBaseMultitenant.Domain.Authentication;

/// <summary>
/// Los formatos del segundo factor (E9): los seis dígitos del autenticador y los códigos de recuperación XXXX-XXXX, que
/// se aceptan en minúsculas o sin guion. Un formato inválido no consume un intento.
/// </summary>
public static class TwoFactorCodeFormat
{
    public const int AuthenticatorDigits = 6;
    public const int RecoveryGroupLength = 4;

    public static bool IsAuthenticatorCode(string? code) =>
        code is { Length: AuthenticatorDigits } && code.All(char.IsAsciiDigit);

    /// <summary>El código de recuperación en mayúsculas y con guion, o null si no tiene ese formato.</summary>
    public static string? NormalizeRecoveryCode(string? code)
    {
        if (code is null) return null;
        var dash = code.IndexOf('-', StringComparison.Ordinal);
        if (dash >= 0 && (dash != RecoveryGroupLength || code.LastIndexOf('-') != dash)) return null;
        var compact = code.Replace("-", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        if (compact.Length != RecoveryGroupLength * 2 || !compact.All(char.IsAsciiLetterOrDigit)) return null;
        return string.Concat(compact.AsSpan(0, RecoveryGroupLength), "-", compact.AsSpan(RecoveryGroupLength));
    }
}
