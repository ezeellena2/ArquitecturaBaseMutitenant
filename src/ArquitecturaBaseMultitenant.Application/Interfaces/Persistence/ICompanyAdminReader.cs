namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Cuenta Administradores efectivos de una empresa en el tenant activo.</summary>
public interface ICompanyAdminReader
{
    Task<int> CountActiveCompanyAdminsAsync(Guid companyId, CancellationToken cancellationToken);
}
