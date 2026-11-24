namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;

/// <summary>
/// Un lugar con un solo dueño entre todas las copias: se toma sin esperar, se devuelve al soltarlo y, si la copia que lo
/// tiene se cae, vence solo. Vive en Redis; sin Redis, el lugar es de la copia (ADR 0058). Lo usan las subidas del Inicio,
/// una por persona (ADR 0064 §4).
/// </summary>
public interface IExclusiveSlots
{
    /// <summary>El lugar <paramref name="holderId"/> de <paramref name="scope"/>, que se suelta al descartarlo; null si otro lo tiene.</summary>
    Task<IAsyncDisposable?> TryTakeAsync(string scope, Guid holderId, CancellationToken cancellationToken);
}
