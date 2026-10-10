using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.AI;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;

public interface IChatClientFactory
{
    Task<Result<IChatClient>> CreateAsync(CancellationToken cancellationToken);
}
