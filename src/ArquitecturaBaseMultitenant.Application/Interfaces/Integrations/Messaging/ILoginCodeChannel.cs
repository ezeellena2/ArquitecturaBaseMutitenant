using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;

/// <summary>Un canal registrado para entregar códigos; nunca envía directamente en el request.</summary>
public interface ILoginCodeChannel
{
    string Key { get; }
    LoginMethodType SupportedMethodType { get; }
    /// <summary>Método técnico para la auditoría de éxito o fallo; un canal heredado conserva Code.</summary>
    LoginAuditMethod AuditMethod => LoginAuditMethod.Code;
    bool IsAvailable { get; }
    IReadOnlyList<string> Countries { get; }
    /// <summary>Número público E.164, nunca un identificador técnico del proveedor.</summary>
    string? ContactPhone { get; }
    /// <summary>Se usa al agregar un destino; un cambio de países no invalida métodos ya verificados.</summary>
    Result ValidateNewDestination(LoginCodeDestination destination, string? country);

    string RenderLoginCode(string destination, string code, int lifetimeMinutes, CultureProfile culture);

    void EnqueueRenderedLoginCode(string payload, Guid? userId = null);

    void EnqueueSignup(string destination, string code, int lifetimeMinutes, CultureProfile culture);
    /// <summary>El código para verificar un destino; sin cuenta en «Recuperar mi cuenta».</summary>
    void EnqueueVerification(Guid? userId, string destination, string code, int lifetimeMinutes, CultureProfile culture);
}
