using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

/// <summary>Identidad global; UserManager escribe solo dentro de la UoW del caso de uso.</summary>
internal sealed class UserRepository(UserManager<ApplicationUser> manager, ApplicationDbContext context) : IUserRepository
{
    public async Task<UserAccountRow> CreateAsync(string? displayName, string culture, string timeZoneId,
        CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        var created = ApplicationUser.Create(displayName, culture, timeZoneId);
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
        string timeZoneId, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        var user = await RequireUserAsync(userId, cancellationToken);
        if (user.Rename(displayName).IsFailure)
        {
            throw new InvalidOperationException("The display name must be validated before persistence.");
        }

        user.UpdatePreferences(culture, timeZoneId);
        EnsureSucceeded(await manager.UpdateAsync(user), "update the profile");
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
            user.LastBusinessTenantId, user.Version, user.DeletionScheduledForUtc);

    private static void EnsureSucceeded(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not {action}: {string.Join(", ", result.Errors.Select(error => error.Code))}.");
        }
    }
}
