using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Resuelve el acceso Personal, Empresa o Plataforma de una cuenta a partir de membresías válidas. Recuerda la última empresa y crea el espacio Personal si aún falta al elegir ese acceso.</summary>
internal sealed class ConnectService(IUserRepository users, IUserTenantAccessReader accesses,
    IPersonalSpaceProvisioner personalSpaces, IPersonalSpaceLock personalSpaceLock,
    ITenantScope tenantScope, IUnitOfWork unitOfWork, TimeProvider timeProvider,
    ILogger<ConnectService> logger) : IConnectService
{
    public Task<Result<ConnectUser>> GetActiveUserAsync(Guid userId, Access access,
        Guid? requestedTenantId, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<ConnectUser>(logger, timeProvider, "GetConnectUser",
            () => ResolveAsync(userId, access, requestedTenantId, cancellationToken));

    public Task<Result<ConnectUser>> SelectAccessAsync(Guid userId, Access access,
        Guid? requestedTenantId, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<ConnectUser>(logger, timeProvider, "SelectAccess", async () =>
        {
            var selected = await ResolveAsync(userId, access, requestedTenantId, cancellationToken);
            if (access == Access.Consumer && selected.IsFailure &&
                selected.Error.Code == AccessErrors.NotMemberCode && requestedTenantId is null)
            {
                return await CreatePersonalSpaceAsync(userId, cancellationToken);
            }

            if (selected.IsFailure || access != Access.Business ||
                selected.Value.Account.LastBusinessTenantId == selected.Value.TenantId)
            {
                return selected;
            }

            return await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var current = await ResolveAsync(userId, access, requestedTenantId, ct);
                if (current.IsFailure)
                {
                    return current;
                }

                await users.RememberBusinessTenantAsync(userId, current.Value.TenantId!.Value, ct);
                return current;
            }, CommitPolicy.OnSuccess, cancellationToken);
        });

    private async Task<Result<ConnectUser>> CreatePersonalSpaceAsync(Guid userId, CancellationToken cancellationToken)
    {
        var account = await users.GetByIdAsync(userId, cancellationToken);
        if (account is null)
        {
            return UserErrors.NotFound;
        }

        var draft = await personalSpaces.PrepareAsync(account.Culture, account.TimeZoneId, cancellationToken);
        using var scope = tenantScope.Enter(draft.Tenant.Id);
        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await personalSpaceLock.LockAsync(userId, ct);
            var current = await ResolveAsync(userId, Access.Consumer, null, ct);
            if (current.IsSuccess || current.Error.Code != AccessErrors.NotMemberCode)
            {
                return current;
            }

            personalSpaces.Stage(draft, userId);
            return new ConnectUser(account, Access.Consumer, draft.Tenant.Id, TenantKind.Personal);
        }, CommitPolicy.OnSuccess, cancellationToken);
    }

    private async Task<Result<ConnectUser>> ResolveAsync(Guid userId, Access access,
        Guid? requestedTenantId, CancellationToken cancellationToken)
    {
        var account = await users.GetByIdAsync(userId, cancellationToken);
        if (account is null || account.Status == UserStatus.Deleted)
        {
            return UserErrors.NotFound;
        }

        if (account.Status == UserStatus.Suspended)
        {
            return AccountErrors.Suspended;
        }

        if (account.Status == UserStatus.PendingDeletion)
        {
            return AccountErrors.PendingDeletion;
        }

        if (access == Access.Platform)
        {
            return account.IsPlatformOperator && requestedTenantId is null
                ? new ConnectUser(account, access, null, null)
                : AccessErrors.Wrong;
        }

        var memberships = await accesses.ListForUserAsync(userId, cancellationToken);
        if (access == Access.Business)
        {
            var selected = AccessSwitchPolicy.SelectBusinessTenant(memberships,
                requestedTenantId, account.LastBusinessTenantId);
            return selected.IsSuccess
                ? new ConnectUser(account, access, selected.Value, TenantKind.Business)
                : selected.Error;
        }

        if (access == Access.Consumer)
        {
            var personal = memberships.SingleOrDefault(member => member.Kind == TenantKind.Personal);
            if (personal is null)
            {
                return AccessErrors.NotMember;
            }

            if (requestedTenantId is not null && requestedTenantId != personal.TenantId)
            {
                return AccessErrors.Wrong;
            }

            if (personal.MemberStatus != MemberStatus.Active)
            {
                return MemberErrors.Inactive;
            }

            return personal.TenantStatus == TenantStatus.Active
                ? new ConnectUser(account, access, personal.TenantId, TenantKind.Personal)
                : TenantErrors.Closed;
        }

        return AccessErrors.Wrong;
    }
}
