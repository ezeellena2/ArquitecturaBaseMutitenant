using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>
/// El aviso a la cuenta encontrada por un pedido de «Recuperar mi cuenta». Lo llama un trabajo en segundo plano, fuera
/// de la respuesta del pedido, para que el tiempo del pedido no delate si la cuenta existe (ADR 0041).
/// </summary>
public interface IAccountRecoveryNoticeService
{
    /// <summary>Encola el aviso de los pedidos registrados que todavía no lo tienen y devuelve cuántos resolvió.</summary>
    Task<Result<int>> NotifyAccountsAsync(CancellationToken cancellationToken);
}
