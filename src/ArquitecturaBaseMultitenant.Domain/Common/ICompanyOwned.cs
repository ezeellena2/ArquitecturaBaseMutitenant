namespace ArquitecturaBaseMultitenant.Domain.Common;

/// <summary>Dato privado de una empresa dentro de una organización.</summary>
public interface ICompanyOwned : ITenantOwned
{
    Guid CompanyId { get; }
}
