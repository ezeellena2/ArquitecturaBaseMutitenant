using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

/// <summary>
/// 0064 §11: la primera copia escribe el control de cada clave del anillo y las demás lo recalculan. No coincide, y la copia no
/// queda lista:
/// - una clave que esta copia usa y cuyo control guardado es otro (otros bytes bajo el mismo id u otro nombre de aplicación);
/// - una clave con control que esta copia no puede abrir (otro certificado): lo que protege sería ilegible acá;
/// - un anillo sin ninguna clave con control cuando ya hay controles (las claves de otra instalación).
/// Una clave sin control que esta copia no abre no es de su anillo y no se mira: la rotación de Data Protection y la clave que
/// generó una copia equivocada no frenan a las demás. Una copia que no coincide no escribe nada, así no se adueña de sus claves,
/// y nunca se borra un control para que pase otro anillo. El lock serializa dos copias que arrancan a la vez.
/// </summary>
internal sealed class AiKeyRingControlService(IAiKeyRingControlRepository repository, IAiKeyRingControl ring, IUnitOfWork unitOfWork,
    TimeProvider clock, ILogger<AiKeyRingControlService> logger) : IAiKeyRingControlService
{
    // Corre una vez por proceso mientras la base responde: la copia guarda el primer veredicto (AiKeyRingReadiness).
    public Task<Result<IReadOnlyList<Guid>>> CheckAsync(CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "AssistantKeyRingCheck", () =>
            unitOfWork.ExecuteInTransactionAsync(CheckCoreAsync, CommitPolicy.OnSuccess, ct));

    private async Task<Result<IReadOnlyList<Guid>>> CheckCoreAsync(CancellationToken ct)
    {
        await repository.LockAsync(ct);
        var stored = (await repository.ListAsync(ct)).ToDictionary(control => control.KeyId, control => control.Control);
        var keys = ring.ReadKeys();
        var mismatched = new SortedSet<Guid>(keys.Readable.Where(keyId => stored.TryGetValue(keyId, out var control) && !ring.Matches(keyId, control)));
        mismatched.UnionWith(keys.Unreadable.Where(stored.ContainsKey));
        // Ya hay controles y ninguna clave que esta copia usa tiene uno: es otro anillo (si no usa ninguna, se nombran las guardadas).
        if (stored.Count > 0 && !keys.Readable.Any(stored.ContainsKey)) mismatched.UnionWith(keys.Readable.Count > 0 ? keys.Readable : stored.Keys);
        if (mismatched.Count > 0) return Result.Success<IReadOnlyList<Guid>>([.. mismatched]);
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        foreach (var keyId in keys.Readable.Where(keyId => !stored.ContainsKey(keyId)).Distinct().Order())
            repository.Add(AiKeyRingControl.Create(keyId, ring.CreateControl(keyId), nowUtc));
        return Result.Success<IReadOnlyList<Guid>>([]);
    }
}
