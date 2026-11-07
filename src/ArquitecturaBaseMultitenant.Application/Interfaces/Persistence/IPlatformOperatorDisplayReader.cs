using ArquitecturaBaseMultitenant.Application.Models.Platform;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>
/// Puerto neutral del núcleo: el nombre y el correo principal de los operadores de Plataforma pedidos, en lote, para una
/// página ya autorizada. Una cuenta que no es operadora o cuya baja empezó no figura.
/// </summary>
public interface IPlatformOperatorDisplayReader
{
    Task<IReadOnlyList<PlatformOperatorDisplayRow>> ListAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
}
