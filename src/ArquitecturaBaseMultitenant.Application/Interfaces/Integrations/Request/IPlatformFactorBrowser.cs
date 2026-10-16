using ArquitecturaBaseMultitenant.Application.Models.Auth;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;

/// <summary>
/// La cookie protegida del desafío pendiente de Plataforma (E9): liga el segundo factor al navegador que probó el
/// primero y no autentica ninguna otra ruta.
/// </summary>
public interface IPlatformFactorBrowser
{
    /// <summary>
    /// El vínculo del desafío, sólo en el host principal y, en una escritura, con el origen público; si no, null.
    /// </summary>
    PlatformFactorBinding? Read();

    /// <summary>Cookie de sesión del navegador: el vencimiento de 10 minutos lo controla el desafío en el servidor.</summary>
    void Write(PlatformFactorBinding binding);

    void Clear();
}
