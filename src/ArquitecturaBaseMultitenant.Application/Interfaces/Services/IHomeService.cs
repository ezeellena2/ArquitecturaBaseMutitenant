using ArquitecturaBaseMultitenant.Application.Models.Home;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>Inicio de la organización activa: tarjetas de las áreas o «Tus empresas», y «Para resolver».</summary>
public interface IHomeService
{
    Task<Result<HomeResponse>> GetAsync(CancellationToken cancellationToken);
}
