using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.Companies;

/// <summary>Guarda una empresa nueva junto con su primera membresía y rol administrador.</summary>
internal sealed class CompanyCreator(
    ICompanyReader reader,
    ICompanyRepository companies,
    ICompanyMembershipRepository memberships,
    IMemberRepository members,
    IRoleRepository roles,
    IRoleAssignmentRepository assignments,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
{
    public Task<Result<Guid>> CreateAsync(Company company, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(ct => CreateCoreAsync(company, ct),
            CommitPolicy.OnSuccess, cancellationToken);

    private async Task<Result<Guid>> CreateCoreAsync(Company company, CancellationToken cancellationToken)
    {
        if (company.TaxId is { } taxId && await reader.ExistsTaxIdAsync(taxId.Country, taxId.Type,
                taxId.Number, excludedCompanyId: null, cancellationToken))
            return CompanyErrors.TaxIdAlreadyUsed;
        if (currentUser.UserId is not { } userId || userId == Guid.Empty)
            return AccessErrors.NotMember;

        var member = await members.GetByUserIdAsync(userId, cancellationToken);
        if (member is null) return AccessErrors.NotMember;
        if (member.Status != MemberStatus.Active) return MemberErrors.Inactive;
        var adminRole = await roles.GetByNameAsync(SystemRoles.CompanyAdmin, cancellationToken);
        if (adminRole is null) throw new InvalidOperationException("The CompanyAdmin system role has not been seeded.");
        var membership = CompanyMembership.Create(company.Id, member.Id);
        if (membership.IsFailure) return membership.Error;
        var assignment = RoleAssignment.Assign(member.Id, adminRole, company.Id);
        if (assignment.IsFailure) return assignment.Error;

        companies.Add(company);
        memberships.Add(membership.Value);
        assignments.Add(assignment.Value);
        return company.Id;
    }
}
