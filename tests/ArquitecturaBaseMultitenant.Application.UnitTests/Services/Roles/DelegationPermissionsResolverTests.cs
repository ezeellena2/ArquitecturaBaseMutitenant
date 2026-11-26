using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Application.Services.Roles;
using ArquitecturaBaseMultitenant.Domain.Authorization;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Roles;

/// <summary>
/// Lo que tiene quien pide y lo que dan unas asignaciones, leído sin un <c>Guard</c> (ADR 0068): lo usan las dos guardas
/// de la 0061, <c>RoleService</c> y las fuentes del asistente, con lo efectivo del catálogo vigente (decisión 260).
/// </summary>
public sealed class DelegationPermissionsResolverTests
{
    private static readonly Guid North = Guid.CreateVersion7();
    private static readonly Guid South = Guid.CreateVersion7();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Reads_what_the_requester_holds_in_the_organization_and_in_each_company_with_what_it_includes()
    {
        const string removedModule = "removed.module.manage";
        var reader = new RolesByPlace();
        reader.Holds([Permissions.Settings.Manage, removedModule]);
        reader.Holds([Permissions.Users.Read]);
        reader.Holds([Permissions.Company.Members.Manage], North);
        reader.Holds([Permissions.Assistant.Use], South);
        var requester = new Requester(Guid.CreateVersion7());

        var held = await new DelegationPermissionsResolver(requester, reader).ReadHeldAsync(Ct);

        Assert.Equal(requester.UserId, reader.AskedFor);
        Assert.Equal(new HashSet<string> { Permissions.Settings.Manage, Permissions.Settings.Read, Permissions.Users.Read },
            held.Organization);
        Assert.Equal(new HashSet<Guid> { North, South }, held.Companies.Keys.ToHashSet());
        Assert.Equal(new HashSet<string> { Permissions.Company.Members.Manage, Permissions.Company.Members.Read },
            held.Companies[North]);
        Assert.Equal(new HashSet<string> { Permissions.Assistant.Use }, held.Companies[South]);
    }

    [Fact]
    public async Task Nobody_signed_in_holds_nothing_and_nothing_is_read()
    {
        var reader = new RolesByPlace();
        reader.Holds(Permissions.All);

        var anonymous = await new DelegationPermissionsResolver(new Requester(null), reader).ReadHeldAsync(Ct);
        var empty = await new DelegationPermissionsResolver(new Requester(Guid.Empty), reader).ReadHeldAsync(Ct);

        Assert.Same(PermissionsByPlace.None, anonymous);
        Assert.Same(PermissionsByPlace.None, empty);
        Assert.Null(reader.AskedFor);
    }

    [Fact]
    public async Task Reads_what_some_assignments_give_by_place_without_looking_at_the_requester()
    {
        var reader = new RolesByPlace();
        var memberId = Guid.CreateVersion7();
        RoleAssignment[] assignments =
        [
            Assign(memberId, reader.Role([Permissions.Users.Manage]), companyId: null),
            Assign(memberId, reader.Role([Permissions.Company.Members.Read]), North),
            Assign(memberId, reader.Role([Permissions.Assistant.Use]), North),
            Assign(memberId, reader.Role([Permissions.Company.Members.Manage]), South),
        ];

        var assigned = await new DelegationPermissionsResolver(new Requester(null), reader)
            .ReadAssignedAsync(assignments, Ct);

        Assert.Null(reader.AskedFor);
        Assert.Equal(new HashSet<string> { Permissions.Users.Manage, Permissions.Users.Read }, assigned.Organization);
        Assert.Equal(new HashSet<string> { Permissions.Company.Members.Read, Permissions.Assistant.Use },
            assigned.Companies[North]);
        Assert.Equal(new HashSet<string> { Permissions.Company.Members.Manage, Permissions.Company.Members.Read },
            assigned.Companies[South]);
        Assert.Empty((await new DelegationPermissionsResolver(new Requester(null), reader).ReadAssignedAsync([], Ct))
            .Companies);
    }

    internal static RoleAssignment Assign(Guid memberId, Guid roleId, Guid? companyId) =>
        RoleAssignment.Assign(memberId, roleId, companyId is null ? RoleScope.Organization : RoleScope.AnyCompany, null,
            companyId).Value;

    /// <summary>Quien pide: con su id, o sin sesión.</summary>
    internal sealed class Requester(Guid? userId) : ICurrentUser
    {
        public Guid? UserId => userId;
        public Domain.Users.Access? Access => Domain.Users.Access.Business;
    }

    /// <summary>
    /// Los roles de quien pide, por lugar, y lo que permite cada rol, como los lee la caché de permisos: sus roles de
    /// organización y, por cada empresa, los de esa empresa.
    /// </summary>
    internal sealed class RolesByPlace : IRoleReader
    {
        private readonly Dictionary<Guid, IReadOnlyList<string>> permissions = [];
        private readonly List<Guid> organization = [];
        private readonly Dictionary<Guid, IReadOnlyList<Guid>> companies = [];

        /// <summary>El usuario por el que se preguntaron los roles; nadie, si no se leyó.</summary>
        public Guid? AskedFor { get; private set; }

        /// <summary>Un rol con estos permisos, que no tiene quien pide.</summary>
        public Guid Role(IReadOnlyCollection<string> codes)
        {
            var roleId = Guid.CreateVersion7();
            permissions[roleId] = [.. codes];
            return roleId;
        }

        /// <summary>Un rol de quien pide: sin empresa, en la organización; con empresa, en esa empresa.</summary>
        public Guid Holds(IReadOnlyCollection<string> codes, Guid? companyId = null)
        {
            var roleId = Role(codes);
            if (companyId is not { } company) organization.Add(roleId);
            else companies[company] = [.. companies.GetValueOrDefault(company) ?? [], roleId];
            return roleId;
        }

        public Task<IReadOnlyList<Guid>> ListOrganizationRoleIdsAsync(Guid userId, CancellationToken cancellationToken)
        {
            AskedFor = userId;
            return Task.FromResult<IReadOnlyList<Guid>>([.. organization]);
        }

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> ListCompanyRoleIdsAsync(Guid userId,
            CancellationToken cancellationToken)
        {
            AskedFor = userId;
            return Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>>(
                new Dictionary<Guid, IReadOnlyList<Guid>>(companies));
        }

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> ListPermissionsByRoleAsync(
            IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<string>>>(permissions
                .Where(role => roleIds.Contains(role.Key))
                .ToDictionary(role => role.Key, role => role.Value));

        public Task<RoleFilterCountsResponse> GetFilterCountsAsync(string? search, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<RoleRow>> ListAllAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<PagedResult<RoleRow>> ListAsync(ListRolesRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<RoleRow?> FindByIdAsync(Guid roleId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<RoleRow>> ListByIdsAsync(IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<bool> ExistsByNameAsync(string name, Guid? companyId, Guid? excludedRoleId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> CountUsersAsync(Guid roleId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
