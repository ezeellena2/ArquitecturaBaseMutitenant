using ArquitecturaBaseMultitenant.Application.Models.Profile;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

public interface IProfileService
{
    Task<Result<MeResponse>> GetAsync(CancellationToken cancellationToken);

    Task<Result> UpdateAsync(UpdateMeRequest request, CancellationToken cancellationToken);
}
