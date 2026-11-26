using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.AssistantActions;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Application.Services.Roles;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.Companies;

/// <summary>
/// La empresa que nombra una acción del asistente (ADR 0068 §5), para todas las fuentes: se resuelve solo con lo que pide
/// la lista de empresas de la pantalla (<c>companies.read</c> en la organización) y solo entre las activas. Sin ese permiso
/// ninguna se resuelve ni se nombra, como si no existiera: el modelo y la tarjeta no prueban ni muestran nombres que la
/// persona no puede listar.
/// </summary>
internal sealed class CompanyActionResolver(ICompanyReader companies)
{
    /// <summary>Si puede ver las empresas, como lo mira <c>[HasPermission]</c> de <c>GET api/companies</c>.</summary>
    internal static bool CanRead(PermissionsByPlace held) =>
        OrganizationDelegationPolicy.InOrganization(held).Contains(Permissions.Companies.Read);

    /// <summary>La empresa activa por el nombre que escribió el modelo, con su duda; sin el permiso o sin parecida, NotFound.</summary>
    internal async Task<Result<(CompanyRow Company, string? Doubt)>> MatchAsync(PermissionsByPlace held, string? text,
        CancellationToken cancellationToken)
    {
        if (!CanRead(held) || !AssistantActionInputs.IsWritten(text)) return CompanyErrors.NotFound;
        var page = await companies.ListAsync(new ListCompaniesRequest
        {
            Search = AssistantActionInputs.SearchOf(text),
            Status = CompanyStatus.Active,
            PageSize = AssistantActionLimits.Candidates,
        }, cancellationToken);
        var match = AssistantActionInputs.Match(page.Items.Where(company => company.Status == CompanyStatus.Active).ToArray(),
            company => [company.Name], text);
        if (match is not { } found) return CompanyErrors.NotFound;
        return (found.Item, found.Doubt);
    }

    /// <summary>La empresa activa de una propuesta guardada, por su id; sin el permiso, null, como si ya no estuviera.</summary>
    internal async Task<CompanyRow?> FindActiveAsync(PermissionsByPlace held, Guid? companyId, CancellationToken cancellationToken) =>
        CanRead(held) && companyId is { } id && await companies.FindByIdAsync(id, cancellationToken) is { Status: CompanyStatus.Active } company
            ? company : null;
}
