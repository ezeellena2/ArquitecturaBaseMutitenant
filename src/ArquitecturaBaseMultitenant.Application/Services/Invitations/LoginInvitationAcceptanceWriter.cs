using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.Invitations;

/// <summary>
/// Acepta todas las reservas del correo o del teléfono demostrado con el código (ADR 0047; el teléfono, decisión 11 de la
/// E11); valida todo antes de crear o activar.
/// </summary>
internal sealed class LoginInvitationAcceptanceWriter(ILoginInvitationRepository invitations,
    InvitationAcceptanceGuard guard, InvitationAccountRegistrar registrar, ITenantReader tenants,
    ILoginMethodReader methods, LoginMethodAvailabilityPolicy availability, TimeProvider clock)
{
    internal Task LockAsync(LoginInvitationsContext context, CancellationToken ct) =>
        guard.LockDestinationAsync(context.Destination, context.ExpectedOwnerId, ct);

    internal async Task<Result<Guid>> AcceptAsync(LoginInvitationsContext context, CancellationToken ct)
    {
        var first = context.Pending[0];
        var owner = await guard.CheckVerifiedOwnerAsync(new LoginInvitationContext(first.InvitationId,
            first.TenantId, context.Destination, context.ExpectedOwnerId), ct);
        if (owner.IsFailure) return owner.Error;
        var rows = await invitations.LockAndReadAsync(context, ct);
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var valid = new List<(Invitation Invitation, Member Member)>();
        foreach (var row in rows)
        {
            // Vencida o revocada durante el pedido no cuenta. Nunca activa su reserva.
            if (row.Invitation.CheckPending(nowUtc).IsFailure) continue;
            var tenant = await tenants.FindByIdAsync(row.Invitation.TenantId, ct);
            if (tenant is null || tenant.Kind != TenantKind.Business) return InvitationErrors.Invalid;
            if (tenant.Status != TenantStatus.Active)
                return tenant.Status == TenantStatus.Suspended ? TenantErrors.Suspended : InvitationErrors.Invalid;
            if (row.Member.Status != MemberStatus.Invited) return InvitationErrors.Invalid;
            if (row.Member.UserId is { } bound && bound != owner.Value) return InvitationErrors.WrongAccount;
            valid.Add(row);
        }
        if (valid.Count == 0) return InvitationErrors.Invalid;
        if (owner.Value is { } existing)
        {
            var ownMethods = await methods.ListByUserIdAsync(existing, ct);
            var type = context.Destination.IsPhone ? LoginMethodType.Phone : LoginMethodType.Email;
            var method = ownMethods.SingleOrDefault(value => value.Type == type && value.Value == context.Destination.Value)
                ?? ownMethods.FirstOrDefault(value => value.Type == LoginMethodType.Google && value.VerifiedAtUtc is not null
                    && value.ContactEmail is { } contact && context.Destination.Email == contact);
            if (method is null || !await availability.CanReceiveCodeAsync(existing, method,
                valid.Select(row => row.Invitation.TenantId).ToArray(), ct))
                return LoginCodeErrors.Invalid(attemptsLeft: null);
        }
        var userId = owner.Value ?? (context.Destination.Email is { } email
            ? await registrar.RegisterVerifiedEmailAsync(context.DefaultsTenantId, email, nowUtc, ct)
            : await registrar.RegisterVerifiedPhoneAsync(context.DefaultsTenantId, context.Destination.Phone!, nowUtc, ct));
        if (owner.Value is not null && context.Destination.Email is { } ensured)
            await registrar.EnsureEmailAsync(userId, context.DefaultsTenantId, ensured, nowUtc, ct);
        await invitations.BindVerifiedUserAsync(userId, ct);
        foreach (var row in valid)
        {
            if (row.Member.UserId is null && row.Member.AssignUser(userId).IsFailure)
                throw new InvalidOperationException("A checked invitation member could not be bound.");
            var accepted = context.Destination.Email is { } proven ? row.Invitation.AcceptVerifiedEmail(userId, proven, nowUtc)
                : row.Invitation.AcceptVerifiedPhone(userId, context.Destination.Phone!, nowUtc);
            if (row.Member.Activate(nowUtc).IsFailure || accepted.IsFailure)
                throw new InvalidOperationException("A checked invitation could not be accepted.");
        }
        return userId;
    }
}
