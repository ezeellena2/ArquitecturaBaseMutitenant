using ArquitecturaBaseMultitenant.Domain.Messaging;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;

/// <summary>
/// La clave de un contador de los topes de mensajes (ADR 0063): un HMAC con la clave compartida entre réplicas, nunca la
/// dirección, la cuenta ni la organización en claro. Una IPv6 cuenta por su /64.
/// </summary>
public interface IMessageQuotaKeyHasher
{
    /// <summary>La de una dirección; sin una dirección legible, una clave fija para todas las desconocidas.</summary>
    string ForAddress(string? ipAddress);

    /// <summary>La de una cuenta o una organización, en su alcance.</summary>
    string ForSubject(MessageQuotaScope scope, Guid id);
}
