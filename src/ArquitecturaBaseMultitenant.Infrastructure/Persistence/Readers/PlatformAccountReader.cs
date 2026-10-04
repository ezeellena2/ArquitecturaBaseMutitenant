using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

/// <summary>
/// Lee la identidad global, sus métodos, el índice de accesos y los ingresos. Nunca consulta Members ni
/// Invitations: las membresías salen de la proyección global. El estado derivado se calcula en SQL con la
/// misma regla que <c>LoginMethodAvailability</c>, para filtrar, contar y mostrar con una sola expresión.
/// </summary>
internal sealed class PlatformAccountReader(ApplicationDbContext context) : IPlatformAccountReader
{
    private static readonly SortMap<AccountProjection> Sort = new()
    {
        ["name"] = account => account.DisplayName,
    };

    public Task<PagedResult<PlatformAccountRow>> ListAsync(ListPlatformAccountsRequest request,
        IReadOnlyCollection<LoginMethodType> signInTypes, CancellationToken ct) =>
        Rows(Filter(Accounts(signInTypes), request).ApplySort(SortDescriptor.Parse(request.Sort), Sort,
            new SortDescriptor("name", false), account => account.Id)).ToPagedResultAsync(request, ct);

    public async Task<PlatformAccountFilterCounts> CountAsync(ListPlatformAccountsRequest request,
        IReadOnlyCollection<LoginMethodType> signInTypes, CancellationToken ct)
    {
        var accounts = Accounts(signInTypes);
        var states = await Filter(accounts, request with { State = null }).GroupBy(account => account.State)
            .Select(group => new PlatformAccountStateCount(group.Key, group.Count())).ToArrayAsync(ct);
        var kinds = await Filter(accounts, request with { Kind = null }).GroupBy(account => account.IsOperator)
            .Select(group => new { IsOperator = group.Key, Count = group.Count() }).ToArrayAsync(ct);
        return new PlatformAccountFilterCounts(await Filter(accounts, request).CountAsync(ct), states,
            [.. kinds.Select(kind => new PlatformAccountKindCount(
                kind.IsOperator ? PlatformAccountKind.Operator : PlatformAccountKind.Person, kind.Count))]);
    }

    public async Task<PlatformAccountDetailRow?> FindAsync(Guid accountId, IReadOnlyCollection<LoginMethodType> signInTypes,
        CancellationToken ct)
    {
        var account = await Rows(Accounts(signInTypes).Where(row => row.Id == accountId)).SingleOrDefaultAsync(ct);
        if (account is null) return null;
        var identity = await context.Users.AsNoTracking().Where(user => user.Id == accountId)
            .Select(user => new
            {
                user.CreatedAtUtc,
                user.DeletionRequestedAtUtc,
                user.DeletionScheduledForUtc,
                RequestedByPlatform = user.DeletionRequestedByOperatorId != null,
            }).SingleAsync(ct);
        var organizations = await (
            from access in context.UserTenantAccesses.AsNoTracking()
            join tenant in context.Tenants.AsNoTracking() on access.TenantId equals tenant.Id
            where access.UserId == accountId && tenant.Kind == TenantKind.Business && access.Status != MemberStatus.Invited
            orderby tenant.Name, tenant.Id
            select new PlatformAccountMembershipResponse(tenant.Id, tenant.Name, access.RoleName, access.JoinedAtUtc,
                access.Status, tenant.Status, access.Status == MemberStatus.Active ? null
                    : context.SecurityEvents.Where(evidence => evidence.Type == SecurityEventType.OrganizationMembershipEnded
                            && evidence.TargetAccountId == accountId && evidence.TargetTenantId == tenant.Id)
                        .OrderByDescending(evidence => evidence.OccurredAtUtc)
                        .Select(evidence => (DateTime?)evidence.OccurredAtUtc).FirstOrDefault()))
            .ToArrayAsync(ct);
        var assignment = account.IsOperator
            ? await context.PlatformPermissionAssignments.AsNoTracking().SingleOrDefaultAsync(row => row.UserId == accountId, ct)
            : null;
        return new PlatformAccountDetailRow(account, identity.CreatedAtUtc, identity.DeletionRequestedAtUtc,
            identity.DeletionScheduledForUtc, identity.RequestedByPlatform, organizations,
            assignment is null ? null : [.. assignment.Permissions]);
    }

    private IQueryable<AccountProjection> Accounts(IReadOnlyCollection<LoginMethodType> signInTypes)
    {
        var email = signInTypes.Contains(LoginMethodType.Email);
        var google = signInTypes.Contains(LoginMethodType.Google);
        var phone = signInTypes.Contains(LoginMethodType.Phone);
        return from user in context.Users.AsNoTracking()
               let usable = context.LoginMethods.Any(method => method.UserId == user.Id && method.VerifiedAtUtc != null
                   && ((method.Type == LoginMethodType.Email && email) || (method.Type == LoginMethodType.Google && google)
                       || (method.Type == LoginMethodType.Phone && phone))
                   && (method.ManagedByTenantId == null || context.UserTenantAccesses.Any(access => access.UserId == user.Id
                       && access.TenantId == method.ManagedByTenantId && access.Status == MemberStatus.Active
                       && context.Tenants.Any(tenant => tenant.Id == access.TenantId && tenant.Status == TenantStatus.Active))))
               select new AccountProjection
               {
                   Id = user.Id,
                   DisplayName = user.DisplayName,
                   Email = user.Email,
                   IsOperator = user.IsPlatformOperator,
                   Version = user.Version,
                   State = user.Status == UserStatus.Active
                       ? (usable ? PlatformAccountState.Active : PlatformAccountState.NeedsRecovery)
                       : user.Status == UserStatus.Suspended ? PlatformAccountState.Suspended
                       : user.Status == UserStatus.PendingDeletion ? PlatformAccountState.PendingDeletion
                       : PlatformAccountState.Deleted,
               };
    }

    private IQueryable<AccountProjection> Filter(IQueryable<AccountProjection> query, ListPlatformAccountsRequest request)
    {
        if (request.State is { } state) query = query.Where(account => account.State == state);
        if (request.Kind is { } kind)
        {
            var isOperator = kind == PlatformAccountKind.Operator;
            query = query.Where(account => account.IsOperator == isOperator);
        }

        // Nombre y métodos de ingreso (correo, teléfono y el correo de contacto de Google), con la misma búsqueda sin
        // acentos y con comodines escapados. La copia ApplicationUser.Email no se usa para buscar (emails.md); nunca se
        // registra el texto buscado.
        if (string.IsNullOrWhiteSpace(request.Search)) return query;
        var byName = context.Users.AsNoTracking()
            .ApplySearch(request.Search, user => user.DisplayName).Select(user => user.Id);
        var byMethod = context.LoginMethods.AsNoTracking().Where(method => method.Type != LoginMethodType.Google)
            .ApplySearch(request.Search, method => method.Value).Select(method => method.UserId);
        var byGoogleContact = context.LoginMethods.AsNoTracking()
            .Where(method => method.Type == LoginMethodType.Google && method.ContactEmail != null)
            .ApplySearch(request.Search, method => EF.Property<string>(method, nameof(LoginMethod.ContactEmail)))
            .Select(method => method.UserId);
        return query.Where(account => byName.Contains(account.Id) || byMethod.Contains(account.Id)
            || byGoogleContact.Contains(account.Id));
    }

    private IQueryable<PlatformAccountRow> Rows(IQueryable<AccountProjection> query) =>
        from account in query
        select new PlatformAccountRow(account.Id, account.DisplayName,
            account.Email == null ? null : Email.Create(account.Email).Value, account.IsOperator, account.State,
            context.UserTenantAccesses.Any(access => access.UserId == account.Id
                && context.Tenants.Any(tenant => tenant.Id == access.TenantId && tenant.Kind == TenantKind.Personal)),
            context.UserTenantAccesses.Count(access => access.UserId == account.Id && access.Status == MemberStatus.Active
                && context.Tenants.Any(tenant => tenant.Id == access.TenantId && tenant.Kind == TenantKind.Business
                    && tenant.Status != TenantStatus.Closed)),
            context.LoginAudits.Where(audit => audit.UserId == account.Id && audit.Succeeded)
                .OrderByDescending(audit => audit.OccurredAtUtc).ThenByDescending(audit => audit.Id)
                .Select(audit => (DateTime?)audit.OccurredAtUtc).FirstOrDefault(),
            context.LoginAudits.Where(audit => audit.UserId == account.Id && audit.Succeeded)
                .OrderByDescending(audit => audit.OccurredAtUtc).ThenByDescending(audit => audit.Id)
                .Select(audit => (LoginAuditMethod?)audit.Method).FirstOrDefault(),
            account.Version);

    private sealed class AccountProjection
    {
        public Guid Id { get; init; }
        public string? DisplayName { get; init; }
        public string? Email { get; init; }
        public bool IsOperator { get; init; }
        public PlatformAccountState State { get; init; }
        public uint Version { get; init; }
    }
}
