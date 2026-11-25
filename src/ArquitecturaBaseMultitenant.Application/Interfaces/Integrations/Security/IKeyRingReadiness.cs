namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;

/// <summary>
/// 0064 §11: si el anillo de Data Protection de esta copia coincide con el de la instalación. Lo registra el módulo que guarda
/// los controles del anillo; sin ninguno registrado, la copia sigue como siempre. Un trabajo de fondo que abre lo que protegieron
/// otras copias, o que escribe lo que ellas tienen que abrir (la cola de salida y las colas de un canal), pregunta a todos antes
/// de tomar algo y no toma nada mientras uno diga que no: lo tomado gastaría sus intentos sin poder abrirlo.
/// </summary>
public interface IKeyRingReadiness
{
    /// <summary>False si no coincide y mientras no se pudo comprobar (la próxima llamada vuelve a probar).</summary>
    Task<bool> MatchesAsync(CancellationToken cancellationToken);
}
