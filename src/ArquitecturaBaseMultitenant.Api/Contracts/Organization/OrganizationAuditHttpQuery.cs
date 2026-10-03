using ArquitecturaBaseMultitenant.Application.Models.Auditing;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Organization;

public sealed record OrganizationAuditHttpQuery(string? After, int Limit = 10, string? Period = "30",
    string? Category = null, Guid? ActorId = null, string? Search = null)
{
    public override string ToString() => nameof(OrganizationAuditHttpQuery);

    public ListOrganizationAuditRequest ToModel() => new()
    {
        After = After,
        Limit = Limit,
        Period = Period,
        Category = Category,
        ActorId = ActorId,
        Search = Search,
    };
}
