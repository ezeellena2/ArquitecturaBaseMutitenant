using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Dns;

public interface IDnsTxtLookup
{
    Task<Result<IReadOnlyList<string>>> FindTxtRecordsAsync(string domain, CancellationToken cancellationToken);
}
