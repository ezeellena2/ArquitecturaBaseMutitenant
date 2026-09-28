using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Common.Validation;

/// <summary>Único validador inyectado en un servicio; resuelve los validadores del pedido en el scope actual.</summary>
public interface IRequestValidator
{
    Task<ValidationError?> ValidateAsync<TRequest>(TRequest request, CancellationToken cancellationToken);
}
