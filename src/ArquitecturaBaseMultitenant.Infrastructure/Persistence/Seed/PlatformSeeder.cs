using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;

/// <summary>Siembra ajustes y primer operador sin crearle un acceso Personal.</summary>
internal sealed class PlatformSeeder(
    IPlatformSettingsRepository settings,
    ISecurityEventRepository securityEvents,
    ApplicationDbContext context,
    UserManager<ApplicationUser> users,
    IConfiguration configuration,
    IHostEnvironment environment,
    TimeProvider timeProvider)
{
    internal PlatformOwnerSeed? ReadOwner()
    {
        var configuredEmail = configuration["Seed:PlatformOwner:Email"];
        if (string.IsNullOrWhiteSpace(configuredEmail))
        {
            return null;
        }

        var email = Email.Create(configuredEmail);
        if (email.IsFailure)
        {
            throw new InvalidOperationException("Seed:PlatformOwner:Email is required and must be a valid email address.");
        }

        var displayName = configuration["Seed:PlatformOwner:DisplayName"]?.Trim();
        if (displayName?.Length == 0)
        {
            displayName = null;
        }

        if (displayName?.Length > TextLimits.PersonName)
        {
            throw new InvalidOperationException("Seed:PlatformOwner:DisplayName exceeds the allowed length.");
        }

        return new PlatformOwnerSeed(email.Value, displayName);
    }

    public async Task SeedAsync(PlatformOwnerSeed? owner, ReferenceDataSeedSnapshot references,
        CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        if (await settings.GetAsync(cancellationToken) is null)
        {
            settings.Add(PlatformSettings.Create(
                ConsumerSignupMode.Open, BusinessSignupMode.Open, maxOwnedOrganizations: 1));
            securityEvents.Add(SecurityEvent.ForPlatformSettings(
                AuditActorKind.System, Guid.Empty, "Initial platform settings seed",
                timeProvider.GetUtcNow().UtcDateTime));
        }

        var operatorIds = await users.Users.Where(user => user.IsPlatformOperator)
            .Select(user => user.Id).ToArrayAsync(cancellationToken);
        if (operatorIds.Length > 0)
        {
            foreach (var userId in operatorIds)
                await EnsureAssignmentAsync(userId, cancellationToken);
            return;
        }

        if (owner is null)
        {
            if (!environment.IsEnvironment("Testing"))
            {
                throw new InvalidOperationException(
                    "Seed:PlatformOwner:Email is required when no platform operator exists.");
            }

            return;
        }

        var method = await context.LoginMethods.SingleOrDefaultAsync(login =>
            login.Type == LoginMethodType.Email && login.Value == owner.Email.Value,
            cancellationToken);
        if (method is not null)
        {
            if (method.VerifiedAtUtc is null || method.ManagedByTenantId is not null)
                throw new InvalidOperationException(
                    "Seed:PlatformOwner:Email must reference a verified, unmanaged email method.");
            var existing = await users.Users.SingleAsync(user => user.Id == method.UserId,
                cancellationToken);
            if (!existing.IsPlatformOperator)
            {
                existing.GrantPlatformOperator();
                EnsureSucceeded(await users.UpdateAsync(existing), "mark the platform operator");
                securityEvents.Add(SecurityEvent.ForPlatformOperatorGrant(
                    "Initial platform owner seed", timeProvider.GetUtcNow().UtcDateTime, existing.Id));
            }
            await EnsureAssignmentAsync(existing.Id, cancellationToken);

            return;
        }

        var defaultCulture = references.Cultures.Single(culture => culture.IsEnabled && culture.IsDefault);
        var timeZone = references.Countries.Single(country =>
            country.Code == defaultCulture.CountryCode).DefaultTimeZoneId;
        if (string.IsNullOrWhiteSpace(timeZone))
        {
            throw new InvalidOperationException("The default culture country has no time zone in reference data.");
        }

        var created = ApplicationUser.Create(owner.DisplayName, defaultCulture.Code, timeZone,
            timeProvider.GetUtcNow().UtcDateTime);
        if (created.IsFailure)
        {
            throw new InvalidOperationException("Seed:PlatformOwner:DisplayName is invalid.");
        }

        var user = created.Value;
        user.GrantPlatformOperator();
        user.Email = owner.Email.Value;
        user.EmailConfirmed = true;
        EnsureSucceeded(await users.CreateAsync(user), "create the platform operator");

        var loginMethod = LoginMethod.CreateEmail(user.Id, owner.Email);
        loginMethod.Verify(timeProvider.GetUtcNow().UtcDateTime);
        if (loginMethod.MakePrimary().IsFailure)
        {
            throw new InvalidOperationException("The platform operator email could not become primary.");
        }

        context.LoginMethods.Add(loginMethod);
        context.PlatformPermissionAssignments.Add(CompleteCatalog(user.Id));
        securityEvents.Add(SecurityEvent.ForPlatformOperatorGrant(
            "Initial platform owner seed", timeProvider.GetUtcNow().UtcDateTime, user.Id));
    }

    // Un operador sin asignación (el primero o un indicador anterior a E5) recibe el catálogo completo;
    // una asignación existente se conserva tal cual.
    private async Task EnsureAssignmentAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!await context.PlatformPermissionAssignments.AnyAsync(assignment => assignment.UserId == userId, cancellationToken))
            context.PlatformPermissionAssignments.Add(CompleteCatalog(userId));
    }

    private static PlatformPermissionAssignment CompleteCatalog(Guid userId) =>
        PlatformPermissionAssignment.Create(userId, PlatformPermissions.All).Value;

    private static void EnsureSucceeded(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not {action}: {string.Join(", ", result.Errors.Select(error => error.Code))}.");
        }
    }
}

internal sealed record PlatformOwnerSeed(Email Email, string? DisplayName);
