using ArquitecturaBaseMultitenant.Application.Common.Formatting;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;

/// <summary>Un canal registrado para entregar códigos; nunca envía directamente en el request.</summary>
public interface ILoginCodeChannel
{
    string Key { get; }

    string RenderLoginCode(string destination, string code, int lifetimeMinutes, CultureProfile culture);

    void EnqueueRenderedLoginCode(string payload, Guid? userId = null);

    void EnqueueSignup(string destination, string code, int lifetimeMinutes, CultureProfile culture);
    /// <summary>El código para verificar un destino; sin cuenta en «Recuperar mi cuenta».</summary>
    void EnqueueVerification(Guid? userId, string destination, string code, int lifetimeMinutes, CultureProfile culture);
}
