using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;

internal sealed record DesiredOrganizationUserAssignments(IReadOnlyList<RoleRow> OrganizationRoles,
    IReadOnlyDictionary<Guid, (CompanyRow Company, IReadOnlyList<RoleRow> Roles)> CompanyRoles);

internal sealed record OrganizationUserAssignmentSnapshot(
    IReadOnlyList<RoleAssignment> Assignments,
    IReadOnlyList<CompanyMembership> CompanyMemberships);
