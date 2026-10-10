using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Engagement;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Engagement;

public interface IConsumerInteractionSource
{
    string Key { get; }
    Task<Result<PagedResult<ConsumerInteractionResponse>>> ListAsync(ListConsumerInteractionsRequest request, CancellationToken cancellationToken);
}
