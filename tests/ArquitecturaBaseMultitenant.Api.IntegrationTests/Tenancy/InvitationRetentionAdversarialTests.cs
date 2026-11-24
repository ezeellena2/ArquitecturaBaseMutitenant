using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

/// <summary>
/// Revisión adversarial de la B2 (ADR 0062): una invitación cerrada se borra a los 30 días, salvo la última de cada
/// destino, en cada organización. Una organización suspendida o cerrada sigue guardando el correo de quien invitó: la
/// retención de un dato personal no depende de que la organización esté activa.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class InvitationRetentionAdversarialTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(TenantStatus.Suspended)]
    [InlineData(TenantStatus.Closed)]
    public async Task Closed_invitations_of_an_organization_that_is_not_active_are_also_purged(TenantStatus status)
    {
        using var client = factory.CreateClient();
        var nowUtc = Now();
        var (tenantId, ownerId) = await OrganizationAsync();
        var destination = Email.Create($"no-activa-{Guid.NewGuid():N}@example.test").Value;
        var (member, old) = await InviteAsync(tenantId, ownerId, null, destination);
        await RevokeAsync(tenantId, old);
        var (_, last) = await InviteAsync(tenantId, ownerId, member, destination);
        await MoveAsync(tenantId, old, issuedAtUtc: nowUtc.AddDays(-45), revokedAtUtc: nowUtc.AddDays(-40));
        await ChangeStatusAsync(tenantId, status);

        // El trabajo de fondo de todos los días, como corre en producción.
        await ActivatorUtilities.CreateInstance<InvitationRetentionWorker>(factory.Services).RunOnceAsync(Ct);

        Assert.Equal([last], await RemainingAsync(tenantId, [old, last]));
    }

    private DateTime Now() => factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;

    private async Task<(Guid TenantId, Guid OwnerId)> OrganizationAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var ownerId = Guid.Empty;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async work =>
        {
            ownerId = (await services.GetRequiredService<IUserRepository>().CreateAsync("Dueña", "es-AR",
                "America/Argentina/Buenos_Aires", work)).Id;
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory,
            new AccountJourney(ownerId, Email.Create($"duena-{Guid.NewGuid():N}@example.test").Value),
            $"Retención no activa {Guid.NewGuid():N}", Ct);
        return (tenantId, ownerId);
    }

    private async Task ChangeStatusAsync(Guid tenantId, TenantStatus status)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var tenant = (await services.GetRequiredService<ITenantRepository>().GetByIdAsync(tenantId, ct))!;
            var suspended = tenant.Suspend();
            if (suspended.IsFailure || status == TenantStatus.Suspended) return suspended;
            return tenant.Close();
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(result.IsSuccess);
    }

    private async Task<(Guid MemberId, Guid InvitationId)> InviteAsync(Guid tenantId, Guid inviterId, Guid? memberId,
        Email destination)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        var invitationId = Guid.Empty;
        var invitedMemberId = memberId ?? Guid.Empty;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var members = services.GetRequiredService<IMemberRepository>();
            var member = memberId is { } id ? await members.GetByIdAsync(id, ct) : null;
            if (member is null)
            {
                member = Member.Invite();
                members.Add(member);
            }

            var invitation = Invitation.Issue(member.Id, inviterId, InvitationIdentity.ForEmail(destination),
                InvitationChannel.Email, $"hash-{Guid.NewGuid():N}", Now(), TimeSpan.FromDays(7));
            services.GetRequiredService<IInvitationRepository>().Add(invitation);
            invitedMemberId = member.Id;
            invitationId = invitation.Id;
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        return (invitedMemberId, invitationId);
    }

    private async Task RevokeAsync(Guid tenantId, Guid invitationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            (await services.GetRequiredService<IInvitationRepository>().GetByIdAsync(invitationId, ct))!.Revoke(Now()),
            CommitPolicy.OnSuccess, Ct);
        Assert.True(result.IsSuccess);
    }

    private async Task MoveAsync(Guid tenantId, Guid invitationId, DateTime issuedAtUtc, DateTime revokedAtUtc)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        Assert.Equal(1, await services.GetRequiredService<ApplicationDbContext>().Invitations
            .Where(invitation => invitation.Id == invitationId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(invitation => invitation.IssuedAtUtc, issuedAtUtc)
                .SetProperty(invitation => invitation.ExpiresAtUtc, issuedAtUtc.AddDays(7))
                .SetProperty(invitation => invitation.RevokedAtUtc, revokedAtUtc), Ct));
    }

    private async Task<Guid[]> RemainingAsync(Guid tenantId, Guid[] invitationIds)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        var remaining = await services.GetRequiredService<ApplicationDbContext>().Invitations.AsNoTracking()
            .Where(invitation => invitationIds.Contains(invitation.Id)).Select(invitation => invitation.Id)
            .ToArrayAsync(Ct);
        return [.. invitationIds.Where(remaining.Contains)];
    }
}
