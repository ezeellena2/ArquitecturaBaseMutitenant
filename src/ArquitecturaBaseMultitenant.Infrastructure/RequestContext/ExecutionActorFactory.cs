using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Models.Request;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Infrastructure.RequestContext;

/// <summary>Relee sólo cuenta, catálogos e índice global antes de habilitar el binding privado del worker.</summary>
internal sealed class ExecutionActorFactory(ICurrentUser currentUser, ITenantContext tenant,
    IUserRepository accounts, IUserStatusReader identities, IUserTenantAccessReader accesses,
    ICultureCatalog cultures, ITimeZoneCatalog zones, ExecutionActorContext context) : IExecutionActorFactory
{
    public Task<Result<ExecutionActor>> CaptureAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return currentUser.UserId is { } owner && currentUser.Access is { } access
            ? ReadAsync(owner, access, tenant.TenantId, tenant.TenantKind, currentUser.SessionStartedAtUtc, cancellationToken)
            : Task.FromResult(Result.Failure<ExecutionActor>(AccessErrors.Wrong));
    }

    public Task<Result<ExecutionActor>> RevalidateAsync(ExecutionActor actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        cancellationToken.ThrowIfCancellationRequested();
        context.EnsureCanEnter();
        return ReadAsync(actor.OwnerUserId, actor.Access, actor.TenantId, actor.TenantKind, actor.SessionStartedAtUtc, cancellationToken);
    }

    private async Task<Result<ExecutionActor>> ReadAsync(Guid owner, Access access, Guid? tenantId, TenantKind? kind,
        DateTime? sessionStartedAtUtc, CancellationToken ct)
    {
        if (owner == Guid.Empty || !Enum.IsDefined(access)) return AccessErrors.Wrong;
        var account = await accounts.GetByIdAsync(owner, ct);
        if (account is null || account.Status != UserStatus.Active || account.HasPendingOperatorInvitation
            || await identities.GetStatusAsync(owner, ct) != UserStatus.Active
            || account.IsPlatformOperator != (access == Access.Platform)) return AccessErrors.Wrong;
        var memberships = await accesses.ListForUserAsync(owner, ct);
        if (access == Access.Platform)
        {
            if (tenantId is not null || kind is not null || memberships.Count != 0) return AccessErrors.Wrong;
        }
        else
        {
            var expected = access == Access.Consumer ? TenantKind.Personal : TenantKind.Business;
            var membership = memberships.SingleOrDefault(row => row.TenantId == tenantId);
            if (membership is not { TenantStatus: TenantStatus.Active, MemberStatus: MemberStatus.Active }
                || membership.Kind != expected || kind != membership.Kind) return AccessErrors.NotMember;
            kind = membership.Kind;
        }
        // Preferencias persistidas: una referencia histórica deshabilitada sigue siendo legible.
        if (await cultures.FindAsync(account.Culture, ct) is null || await zones.FindAsync(account.TimeZoneId, ct) is null)
            return AccessErrors.Wrong;
        return new ExecutionActor(owner, access, tenantId, account.Culture, account.TimeZoneId)
        {
            TenantKind = kind,
            SessionStartedAtUtc = sessionStartedAtUtc,
        };
    }
}
