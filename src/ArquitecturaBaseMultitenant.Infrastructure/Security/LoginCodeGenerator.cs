using ArquitecturaBaseMultitenant.Application.Configuration.Auth;
using System.Security.Cryptography;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Security;

/// <summary>Genera el código numérico aleatorio que se entrega por un canal de ingreso. La longitud viene de configuración; su hash y verificación viven en otras piezas.</summary>
internal sealed class LoginCodeGenerator(IOptions<LoginCodeOptions> options) : ILoginCodeGenerator
{
    private const string Digits = "0123456789";

    public string Generate() => RandomNumberGenerator.GetString(Digits, options.Value.Length);
}
