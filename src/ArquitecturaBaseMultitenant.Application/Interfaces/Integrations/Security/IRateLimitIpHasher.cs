namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;

/// <summary>La partición de una IP para las cuotas (E9): un HMAC con clave compartida entre réplicas, nunca la IP completa.</summary>
public interface IRateLimitIpHasher
{
    string Hash(string? ipAddress);
}
