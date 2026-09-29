using ArquitecturaBaseMultitenant.Application.Configuration.Auth;
using System.Security.Cryptography;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Security;

internal sealed class LoginCodeGenerator(IOptions<LoginCodeOptions> options) : ILoginCodeGenerator
{
    private const string Digits = "0123456789";

    public string Generate() => RandomNumberGenerator.GetString(Digits, options.Value.Length);
}
