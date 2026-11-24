using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Auditing;
using ArquitecturaBaseMultitenant.Domain.Auditing;
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
/// ADR 0062: las invitaciones cerradas (aceptadas, revocadas o vencidas) se borran a los 30 días, salvo la última de cada
/// destino de cada persona invitada, y nunca una pendiente. Cada organización se purga en su propio alcance, con
/// <see cref="TenantJobRunner"/>, y la auditoría de la organización sigue nombrando a quien no tiene nombre.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class InvitationRetentionTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Closed_invitations_are_removed_after_thirty_days_except_the_last_one_and_never_a_pending_one()
    {
        using var client = factory.CreateClient();
        var nowUtc = Now();
        var (tenantId, ownerId) = await OrganizationAsync();
        var resent = Mail("reenviada");

        // Reenviada dos veces: las dos primeras se cerraron hace más de 30 días y la última sigue pendiente.
        var (member, first) = await InviteAsync(tenantId, ownerId, null, resent);
        await RevokeAsync(tenantId, first);
        var (_, second) = await InviteAsync(tenantId, ownerId, member, resent);
        await RevokeAsync(tenantId, second);
        var (_, pending) = await InviteAsync(tenantId, ownerId, member, resent);
        await MoveAsync(tenantId, first, issuedAtUtc: nowUtc.AddDays(-45), revokedAtUtc: nowUtc.AddDays(-40));
        await MoveAsync(tenantId, second, issuedAtUtc: nowUtc.AddDays(-38), revokedAtUtc: nowUtc.AddDays(-35));

        // Cerrada hace 31 días y hace 20, con otra después: la de 20 todavía no cumplió el plazo.
        var recent = Mail("plazo");
        var (recentMember, closedLongAgo) = await InviteAsync(tenantId, ownerId, null, recent);
        await RevokeAsync(tenantId, closedLongAgo);
        var (_, closedRecently) = await InviteAsync(tenantId, ownerId, recentMember, recent);
        await RevokeAsync(tenantId, closedRecently);
        var (_, accepted) = await InviteAsync(tenantId, ownerId, recentMember, recent);
        await MoveAsync(tenantId, closedLongAgo, issuedAtUtc: nowUtc.AddDays(-33), revokedAtUtc: nowUtc.AddDays(-31));
        await MoveAsync(tenantId, closedRecently, issuedAtUtc: nowUtc.AddDays(-21), revokedAtUtc: nowUtc.AddDays(-20));
        await AcceptAsync(tenantId, accepted, ownerId, nowUtc.AddDays(-19));

        // La única de su persona, cerrada hace dos meses: queda.
        var (_, onlyOne) = await InviteAsync(tenantId, ownerId, null, Mail("unica"));
        await RevokeAsync(tenantId, onlyOne);
        await MoveAsync(tenantId, onlyOne, issuedAtUtc: nowUtc.AddDays(-65), revokedAtUtc: nowUtc.AddDays(-60));

        // Vencida pendiente hace dos meses: una pendiente nunca se borra.
        var (_, expired) = await InviteAsync(tenantId, ownerId, null, Mail("vencida"));
        await MoveAsync(tenantId, expired, issuedAtUtc: nowUtc.AddDays(-67), revokedAtUtc: null);

        // Revocada ya vencida, con su vencimiento todavía sin registrar en la auditoría: espera a que se registre.
        var unrecorded = Mail("sin-registrar");
        var (unrecordedMember, revokedExpired) = await InviteAsync(tenantId, ownerId, null, unrecorded);
        await RevokeAsync(tenantId, revokedExpired);
        await InviteAsync(tenantId, ownerId, unrecordedMember, unrecorded);
        await MoveAsync(tenantId, revokedExpired, issuedAtUtc: nowUtc.AddDays(-50), revokedAtUtc: nowUtc.AddDays(-40));

        // El mismo destino invitado de nuevo como otra persona: la anterior es la última de la suya.
        var reused = Mail("otra-persona");
        var (_, ofTheFirstPerson) = await InviteAsync(tenantId, ownerId, null, reused);
        await RevokeAsync(tenantId, ofTheFirstPerson);
        await InviteAsync(tenantId, ownerId, null, reused);
        await MoveAsync(tenantId, ofTheFirstPerson, issuedAtUtc: nowUtc.AddDays(-45), revokedAtUtc: nowUtc.AddDays(-40));

        await PurgeAsync(tenantId);
        await PurgeAsync(tenantId);

        Assert.Equal(
            [pending, closedRecently, accepted, onlyOne, expired, revokedExpired, ofTheFirstPerson],
            await RemainingAsync(tenantId, [first, second, pending, closedLongAgo, closedRecently, accepted, onlyOne,
                expired, revokedExpired, ofTheFirstPerson]));
    }

    [Fact]
    public async Task Each_organization_is_purged_in_its_own_scope()
    {
        using var client = factory.CreateClient();
        var nowUtc = Now();
        var (alpha, alphaOwner) = await OrganizationAsync();
        var (beta, betaOwner) = await OrganizationAsync();
        var shared = Mail("dos-organizaciones");
        var alphaOld = await ClosedThenPendingAsync(alpha, alphaOwner, shared, nowUtc);
        var betaOld = await ClosedThenPendingAsync(beta, betaOwner, shared, nowUtc);

        await PurgeAsync(alpha);

        Assert.Empty(await RemainingAsync(alpha, [alphaOld]));
        Assert.Equal([betaOld], await RemainingAsync(beta, [betaOld]));

        // El trabajo de fondo recorre las organizaciones, cada una en su alcance.
        await ActivatorUtilities.CreateInstance<InvitationRetentionWorker>(factory.Services).RunOnceAsync(Ct);

        Assert.Empty(await RemainingAsync(beta, [betaOld]));
    }

    [Fact]
    public async Task The_organization_audit_still_names_whoever_was_invited_after_the_purge()
    {
        using var client = factory.CreateClient();
        var nowUtc = Now();
        var (tenantId, ownerId) = await OrganizationAsync();
        var destination = Mail("auditoria");
        var (member, first) = await InviteAsync(tenantId, ownerId, null, destination);
        await RevokeAsync(tenantId, first);
        var (_, last) = await InviteAsync(tenantId, ownerId, member, destination);
        await MoveAsync(tenantId, first, issuedAtUtc: nowUtc.AddDays(-45), revokedAtUtc: nowUtc.AddDays(-40));
        var named = await AffectedNamesAsync(tenantId, first);

        await PurgeAsync(tenantId);

        Assert.Empty(await RemainingAsync(tenantId, [first]));
        Assert.Equal([last], await RemainingAsync(tenantId, [last]));
        // «Invitó a …» y «Revocó la invitación a …» siguen nombrando a la persona por el correo de su invitación.
        var afterPurge = await AffectedNamesAsync(tenantId, first);
        Assert.Equal(2, afterPurge.Count);
        Assert.Equal(named, afterPurge);
        Assert.All(afterPurge, name => Assert.False(string.IsNullOrWhiteSpace(name)));
    }

    private async Task<Guid> ClosedThenPendingAsync(Guid tenantId, Guid ownerId, Email destination, DateTime nowUtc)
    {
        var (member, closed) = await InviteAsync(tenantId, ownerId, null, destination);
        await RevokeAsync(tenantId, closed);
        await InviteAsync(tenantId, ownerId, member, destination);
        await MoveAsync(tenantId, closed, issuedAtUtc: nowUtc.AddDays(-45), revokedAtUtc: nowUtc.AddDays(-40));
        return closed;
    }

    private Task PurgeAsync(Guid tenantId) => factory.Services.GetRequiredService<TenantJobRunner>().RunAsync([tenantId],
        ActivatorUtilities.CreateInstance<InvitationRetentionWorker>(factory.Services).PurgeOrganizationAsync, Ct);

    private DateTime Now() => factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;

    private static Email Mail(string name) => Email.Create($"{name}-{Guid.NewGuid():N}@example.test").Value;

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
        var tenantId = await OrganizationJourney.StageAsync(factory, new AccountJourney(ownerId, Mail("duena")),
            $"Retención {Guid.NewGuid():N}", Ct);
        return (tenantId, ownerId);
    }

    /// <summary>Invita a una persona nueva o a <paramref name="memberId"/>, como Usuarios: el miembro y su invitación juntos.</summary>
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

    private Task RevokeAsync(Guid tenantId, Guid invitationId) => ChangeAsync(tenantId, invitationId,
        invitation => invitation.Revoke(Now()));

    private async Task ChangeAsync(Guid tenantId, Guid invitationId, Func<Invitation, Result> change)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            change((await services.GetRequiredService<IInvitationRepository>().GetByIdAsync(invitationId, ct))!),
            CommitPolicy.OnSuccess, Ct);
        Assert.True(result.IsSuccess);
    }

    /// <summary>Lleva la invitación al pasado, como si hubiera pasado el tiempo: vive siete días desde que se mandó.</summary>
    private async Task MoveAsync(Guid tenantId, Guid invitationId, DateTime issuedAtUtc, DateTime? revokedAtUtc)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        var expiresAtUtc = issuedAtUtc.AddDays(7);
        // Vencida pendiente: su vencimiento ya quedó en la auditoría, como lo deja InvitationExpiryWorker.
        DateTime? recordedAtUtc = revokedAtUtc is null ? expiresAtUtc.AddHours(1) : null;
        Assert.Equal(1, await services.GetRequiredService<ApplicationDbContext>().Invitations
            .Where(invitation => invitation.Id == invitationId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(invitation => invitation.IssuedAtUtc, issuedAtUtc)
                .SetProperty(invitation => invitation.ExpiresAtUtc, expiresAtUtc)
                .SetProperty(invitation => invitation.RevokedAtUtc, revokedAtUtc)
                .SetProperty(invitation => invitation.ExpiryRecordedAtUtc, recordedAtUtc), Ct));
    }

    private async Task AcceptAsync(Guid tenantId, Guid invitationId, Guid userId, DateTime acceptedAtUtc)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        Assert.Equal(1, await services.GetRequiredService<ApplicationDbContext>().Invitations
            .Where(invitation => invitation.Id == invitationId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(invitation => invitation.Status, InvitationStatus.Accepted)
                .SetProperty(invitation => invitation.AcceptedByUserId, userId)
                .SetProperty(invitation => invitation.AcceptedAtUtc, acceptedAtUtc)
                .SetProperty(invitation => invitation.IssuedAtUtc, acceptedAtUtc.AddDays(-1))
                .SetProperty(invitation => invitation.ExpiresAtUtc, acceptedAtUtc.AddDays(6)), Ct));
    }

    /// <summary>Cuáles de esas invitaciones siguen, en el orden pedido.</summary>
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

    /// <summary>A quién nombra cada entrada de la auditoría de esa invitación, de la más vieja a la más nueva.</summary>
    private async Task<List<string?>> AffectedNamesAsync(Guid tenantId, Guid invitationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        var page = await services.GetRequiredService<IOrganizationAuditReader>().ListAsync(
            new ListOrganizationAuditRequest { Period = "all", Limit = CursorRequest.MaxLimit },
            null, null, Ct);
        return [.. page.Items.Where(row => row.EntityType == nameof(Invitation) && row.EntityId == invitationId)
            .OrderBy(row => row.Action == AuditAction.Created ? 0 : 1).Select(row => row.AffectedDisplayName)];
    }
}
