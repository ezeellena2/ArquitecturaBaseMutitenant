using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence;

/// <summary>
/// 0064 §6: lo que esta versión sabe leer de un mensaje. Una conversación con un rol o un estado que escribió una versión más
/// nueva no se puede leer: no se arma, no se exporta ni recibe un turno, y se puede borrar. Lo usan el listado, el GET y el
/// historial del turno, en SQL, sin materializar el mensaje.
/// </summary>
internal static class AssistantKnownMessages
{
    internal static readonly string[] Roles = Enum.GetNames<AssistantMessageRole>();
    internal static readonly AssistantPersistedMessageStatus[] Statuses = Enum.GetValues<AssistantPersistedMessageStatus>();
}
