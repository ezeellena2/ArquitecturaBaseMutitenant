using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools.Business;

internal sealed class CompanyMembersTool(AssistantAccessPolicy policy, ICompanyMembershipService members) : AssistantTool(policy)
{
    public override string Key => "org_company_members";
    public override Access? Access => Domain.Users.Access.Business;
    public override IReadOnlyList<string> Permissions => [Domain.Authorization.Permissions.Company.Members.Read];
    protected override IReadOnlyList<AssistantToolParameter> Parameters => [new("companyId", "uuid", true), new("status", "string", Values: Enum.GetNames<CompanyMemberState>()), Page, PageSize];
    protected override async Task<Result<AssistantToolData>> ExecuteCoreAsync(AssistantToolArguments args, CancellationToken ct)
    {
        var result = await members.ListAsync(args.Id("companyId")!.Value, new ListCompanyMembersRequest { State = args.EnumValue<CompanyMemberState>("status"), Page = args.Page, PageSize = args.PageSize }, ct);
        return result.IsFailure ? result.Error : new AssistantQuickToolData(new AssistantPeopleQuickDataResponse([.. result.Value.Items.Take(20).Select(row => new AssistantPersonRowResponse(row.MemberId, row.DisplayName, row.State.ToString()))], result.Value.TotalCount, result.Value.HasNext));
    }
}
