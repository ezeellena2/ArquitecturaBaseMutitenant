using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

/// <summary>Identidad global; UserManager escribe solo dentro de la UoW del caso de uso.</summary>
internal sealed class UserRepository(UserManager<ApplicationUser> manager, ApplicationDbContext context,
    TimeProvider timeProvider) : IUserRepository
{
    public Task<Result> RequestDeletionAsync(Guid userId, string reason, DateTime requestedAtUtc, int graceDays,
        CancellationToken cancellationToken) => RequestAsync(userId, reason, requestedAtUtc, graceDays, null, cancellationToken);

    public Task<Result> RequestDeletionByOperatorAsync(Guid userId, Guid operatorId, string reason, DateTime requestedAtUtc,
        int graceDays, CancellationToken cancellationToken) =>
        RequestAsync(userId, reason, requestedAtUtc, graceDays, operatorId, cancellationToken);

    private async Task<Result> RequestAsync(Guid userId, string reason, DateTime requestedAtUtc, int graceDays,
        Guid? operatorId, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        var user = await RequireUserAsync(userId, cancellationToken);
        var result = user.RequestDeletion(reason, requestedAtUtc, graceDays, operatorId);
        if (result.IsSuccess) user.SecurityStamp = Guid.CreateVersion7().ToString("N");
        return result;
    }

    public async Task<Result> CancelDeletionAsync(Guid userId, DateTime cancelledAtUtc, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        return (await RequireUserAsync(userId, cancellationToken)).CancelDeletion(cancelledAtUtc);
    }

    public async Task<Result> CompleteDeletionAsync(Guid userId, DateTime deletedAtUtc, string deletedDisplayName,
        CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        return (await RequireUserAsync(userId, cancellationToken)).CompleteDeletion(deletedAtUtc, deletedDisplayName);
    }

    public async Task<Result> SuspendAsync(Guid userId, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        return (await RequireUserAsync(userId, cancellationToken)).Suspend();
    }

    public async Task<Result<UserStatus>> ReactivateAsync(Guid userId, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        var user = await RequireUserAsync(userId, cancellationToken);
        var reactivated = user.Reactivate();
        return reactivated.IsFailure ? reactivated.Error : user.Status;
    }

    public async Task SetPlatformOperatorAsync(Guid userId, bool isOperator, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        var user = await RequireUserAsync(userId, cancellationToken);
        if (isOperator) user.GrantPlatformOperator();
        else user.RevokePlatformOperator();
    }

    public async Task InvitePlatformOperatorAsync(Guid userId, DateTime expiresAtUtc, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        (await RequireUserAsync(userId, cancellationToken)).InvitePlatformOperator(expiresAtUtc);
    }

    public async Task<Result> RenewOperatorInvitationAsync(Guid userId, DateTime expiresAtUtc,
        CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        return (await RequireUserAsync(userId, cancellationToken)).RenewOperatorInvitation(expiresAtUtc);
    }

    public async Task<Result> AcceptOperatorInvitationAsync(Guid userId, DateTime acceptedAtUtc,
        CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        return (await RequireUserAsync(userId, cancellationToken)).AcceptOperatorInvitation(acceptedAtUtc);
    }

    public async Task<Result> DiscardOperatorInvitationAsync(Guid userId, DateTime discardedAtUtc,
        string deletedDisplayName, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        return (await RequireUserAsync(userId, cancellationToken))
            .DiscardOperatorInvitation(discardedAtUtc, deletedDisplayName);
    }

    public async Task<UserAccountRow> CreateAsync(string? displayName, string culture, string timeZoneId,
        CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        var created = ApplicationUser.Create(displayName, culture, timeZoneId, timeProvider.GetUtcNow().UtcDateTime);
        if (created.IsFailure)
        {
            throw new InvalidOperationException("The user must be validated before persistence.");
        }

        var user = created.Value;
        EnsureSucceeded(await manager.CreateAsync(user), "create the account");
        return ToRow(user);
    }

    public async Task<UserAccountRow?> GetByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await context.Users.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        return user is null ? null : ToRow(user);
    }

    public async Task SetPrimaryEmailAsync(Guid userId, Email email, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);
        context.RequireTransaction();
        var user = await RequireUserAsync(userId, cancellationToken);
        user.Email = email.Value;
        user.EmailConfirmed = true;
        EnsureSucceeded(await manager.UpdateAsync(user), "set the primary email");
    }

    public async Task UpdateProfileAsync(Guid userId, string? displayName, string culture,
        string timeZoneId, uint expectedVersion, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        var user = await RequireUserAsync(userId, cancellationToken);
        if (user.Rename(displayName).IsFailure)
        {
            throw new InvalidOperationException("The display name must be validated before persistence.");
        }

        user.UpdatePreferences(culture, timeZoneId);
        context.Entry(user).Property(candidate => candidate.Version).OriginalValue = expectedVersion;
        // UserStore.Update vuelve a adjuntar la entidad y restablece los valores originales.
        // Nombre y preferencias no requieren normalización de Identity; los guarda la UoW.
    }

    public async Task SetPrimaryContactAsync(Guid userId, Email? email, PhoneNumber? phoneNumber,
        CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        var user = await RequireUserAsync(userId, cancellationToken);
        user.Email = email?.Value;
        user.EmailConfirmed = email is not null;
        user.PhoneNumber = phoneNumber?.Value;
        user.PhoneNumberConfirmed = phoneNumber is not null;
        EnsureSucceeded(await manager.UpdateAsync(user), "set the primary contact");
    }

    public async Task RememberBusinessTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        var user = await RequireUserAsync(userId, cancellationToken);
        user.RememberBusinessTenant(tenantId);
        EnsureSucceeded(await manager.UpdateAsync(user), "remember the business organization");
    }

    private async Task<ApplicationUser> RequireUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await manager.Users.SingleOrDefaultAsync(user => user.Id == userId, cancellationToken)
        ?? throw new InvalidOperationException("The user does not exist.");

    private static UserAccountRow ToRow(ApplicationUser user) =>
        new(user.Id, user.DisplayName, user.Culture, user.TimeZoneId, user.Status,
            user.IsPlatformOperator, user.Email is null ? null : Email.Create(user.Email).Value,
            user.LastBusinessTenantId, user.Version, user.DeletionScheduledForUtc, user.OperatorInvitationExpiresAtUtc);

    private static void EnsureSucceeded(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            if (result.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.ConcurrencyFailure)))
                throw new ConcurrencyConflictException();
            throw new InvalidOperationException(
                $"Could not {action}: {string.Join(", ", result.Errors.Select(error => error.Code))}.");
        }
    }
}
