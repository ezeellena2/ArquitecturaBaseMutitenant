using ArquitecturaBaseMultitenant.Application.Models.Idempotency;

namespace ArquitecturaBaseMultitenant.Application.Models.Auth;

/// <summary>
/// Las referencias técnicas que el almacén global de idempotencia guarda para las operaciones del segundo factor (E9):
/// el tipo y el id del vínculo durable, nunca códigos, cookies ni respuestas.
/// </summary>
public static class PlatformFactorReferences
{
    public const string Verify = "platform.factor.verify";
    public const string Complete = "platform.factor.complete";
    public const string Reauthentication = "platform.reauthentication";

    public static IReadOnlyList<string> Kinds { get; } = [Verify, Complete, Reauthentication];

    public static IdempotentReference For(string kind, Guid operationId) => new(kind, operationId, 200, null);
}
