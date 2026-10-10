namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Prepara referencias opcionales dentro de la transacción y el lock del seed principal.</summary>
public interface IModuleDataSeedContributor
{
    Task StageAsync(CancellationToken cancellationToken);
}
