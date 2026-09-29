using System.ComponentModel.DataAnnotations;

namespace ArquitecturaBaseMultitenant.Infrastructure.Security;

/// <summary>
/// Clave secreta del HMAC de los códigos (Authentication:LoginCode:HashKey): al menos 32 bytes aleatorios en base64.
/// Se carga desde user-secrets en desarrollo y desde variables de entorno o un almacén de secretos en producción.
/// </summary>
internal sealed class LoginCodeHashOptions : IValidatableObject
{
    public const string SectionName = "Authentication:LoginCode";
    public const int MinKeyBytes = 32;

    [Required]
    public string HashKey { get; init; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var buffer = new byte[HashKey.Length];

        if (!Convert.TryFromBase64String(HashKey, buffer, out var length) || length < MinKeyBytes)
        {
            yield return new ValidationResult(
                "HashKey must be at least 32 random bytes encoded in base64.", [nameof(HashKey)]);
        }
    }
}
