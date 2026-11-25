using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;

/// <summary>
/// 0064 §11: el veredicto del anillo de esta copia contra los controles de la base, calculado al arrancar (el primer chequeo o la
/// primera vuelta del worker) y guardado mientras viva el proceso, porque el anillo se lee al arrancar. Mientras la base no
/// responde no se guarda nada y la próxima llamada vuelve a probar; hasta entonces la copia tampoco está lista. Lo que no
/// coincide se anota una vez, con los ids de las claves, nunca una clave ni un control. Es el <see cref="IKeyRingReadiness"/> de la
/// instalación: con el mismo veredicto, la cola de salida y las de los canales tampoco toman nada.
/// </summary>
internal sealed partial class AiKeyRingReadiness(IServiceScopeFactory scopes, ILogger<AiKeyRingReadiness> logger) : IKeyRingReadiness
{
    private IReadOnlyList<Guid>? mismatched;

    /// <summary>False si no coincide o si todavía no se pudo comprobar.</summary>
    public async Task<bool> MatchesAsync(CancellationToken ct)
    {
        try { return (await CheckAsync(ct)).Count == 0; }
        catch (Exception exception) when (!ct.IsCancellationRequested)
        {
            LogNotVerified(logger, exception.GetType().Name);
            return false;
        }
    }

    /// <summary>Las claves que no coinciden, vacío si el anillo coincide; tira, sin guardar nada, mientras la base no responde.</summary>
    public async Task<IReadOnlyList<Guid>> CheckAsync(CancellationToken ct)
    {
        if (Volatile.Read(ref mismatched) is { } known) return known;
        IReadOnlyList<Guid> found;
        await using (var scope = scopes.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<IAiKeyRingControlService>().CheckAsync(ct);
            if (result.IsFailure) throw new InvalidOperationException("The key ring check failed: " + result.Error.Code);
            found = result.Value;
        }
        if (Interlocked.CompareExchange(ref mismatched, found, null) is null && found.Count > 0) LogRingMismatch(logger, Keys(found));
        return Volatile.Read(ref mismatched)!;
    }

    public static string Keys(IReadOnlyList<Guid> keyIds) => string.Join(", ", keyIds.Select(keyId => "'" + keyId.ToString("D") + "'"));

    [LoggerMessage(Level = LogLevel.Error, Message = "The key ring of this copy does not match the control values in the database for the key {KeyIds}. "
        + "This copy stays out of readiness and takes no assistant turn and no queued work that needs the ring. Load the ring the other copies use (certificate and application "
        + "name) as docs/operations/runbook.md says and restart it; never delete a control.")]
    private static partial void LogRingMismatch(ILogger logger, string keyIds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The key ring could not be checked against the database yet ({ExceptionType}); "
        + "this copy takes no assistant turn and no queued work that needs the ring until it can.")]
    private static partial void LogNotVerified(ILogger logger, string exceptionType);
}
