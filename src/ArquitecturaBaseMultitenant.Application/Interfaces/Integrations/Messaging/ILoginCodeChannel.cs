using ArquitecturaBaseMultitenant.Application.Common.Formatting;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;

/// <summary>Un canal registrado para entregar códigos; nunca envía directamente en el request.</summary>
public interface ILoginCodeChannel
{
    string Key { get; }

    string RenderLoginCode(string destination, string code, int lifetimeMinutes, CultureProfile culture);

    void EnqueueRenderedLoginCode(string payload);

    void EnqueueSignup(string destination, string code, int lifetimeMinutes, CultureProfile culture);
    void EnqueueVerification(Guid userId, string destination, string code, int lifetimeMinutes, CultureProfile culture);
}
