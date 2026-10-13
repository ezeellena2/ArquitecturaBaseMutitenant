using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools.Business;

internal sealed class OrganizationCompaniesTool(AssistantAccessPolicy policy, ICompanyService companies) : AssistantTool(policy)
{
    public override string Key => "org_companies";
    public override Access? Access => Domain.Users.Access.Business;
    public override IReadOnlyList<string> Permissions => [Domain.Authorization.Permissions.Companies.Read];
    protected override IReadOnlyList<AssistantToolParameter> Parameters => [new("status", "string", Values: Enum.GetNames<CompanyStatus>()), new("search", "string"), Page, PageSize];
    protected override async Task<Result<AssistantToolData>> ExecuteCoreAsync(AssistantToolArguments args, CancellationToken ct)
    {
        var result = await companies.ListAsync(new ListCompaniesRequest { Status = args.EnumValue<CompanyStatus>("status"), Search = args.Text("search"), Page = args.Page, PageSize = args.PageSize }, ct);
        return result.IsFailure ? result.Error : new AssistantCompaniesToolData([.. result.Value.Items.Take(AssistantTechnicalLimits.QuickRows).Select(row => new AssistantCompanyToolRow(row.Id, row.Name, row.Status.ToString(), row.MemberCount))], result.Value.TotalCount, result.Value.HasNext);
    }
}
