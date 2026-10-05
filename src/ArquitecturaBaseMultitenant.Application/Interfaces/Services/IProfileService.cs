using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Models.Profile;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>Construye el perfil de la sesión y actualiza sus preferencias globales sin mezclar los accesos de organización.</summary>
public interface IProfileService
{
    Task<Result<MeResponse>> GetAsync(CancellationToken cancellationToken);

    Task<Result<VersionResponse>> UpdateAsync(UpdateMeRequest request, CancellationToken cancellationToken);
}
