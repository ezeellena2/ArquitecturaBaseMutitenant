using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;

/// <summary>Datos de muestra; cada espacio se prepara antes de su propio límite tenant.</summary>
internal sealed class DevelopmentSeeder(
    ApplicationDbContext context,
    IUserLookup lookup,
    IUserRepository users,
    ILoginMethodRepository methods,
    IUserTenantAccessReader accesses,
    IPersonalSpaceLock personalSpaceLock,
    IPersonalSpaceProvisioner personalSpaces,
    TenantSpaceProvisioner spaces,
    IConfiguration configuration,
    TimeProvider timeProvider)
{
    private const string BusinessName = "Empresa A";
    private const string DefaultAnaEmail = "ana@example.test";
    private const string KevinEmail = "kevin@empresa-a.test";
    private const string CarlaEmail = "carla@example.test";

    internal async Task<DevelopmentSeedCandidate> PrepareBusinessAsync(CancellationToken cancellationToken)
    {
        var anaEmail = Email.Create(configuration["Seed:Development:AnaEmail"] ?? DefaultAnaEmail);
        if (anaEmail.IsFailure)
            throw new InvalidOperationException("Seed:Development:AnaEmail must be a valid email address.");

        var existing = await FindBusinessAsync(cancellationToken);
        return new DevelopmentSeedCandidate(existing ?? Tenant.CreateBusiness(BusinessName, requiresApproval: false),
            anaEmail.Value);
    }

    internal async Task StageBusinessAsync(DevelopmentSeedCandidate candidate, ReferenceDataSeedSnapshot references,
        CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        if (await context.Tenants.AsNoTracking().AnyAsync(tenant =>
                tenant.Id == candidate.Tenant.Id, cancellationToken))
        {
            return;
        }

        // Otro seed pudo confirmar Empresa A después de PrepareBusinessAsync.
        if (await lookup.FindMethodAsync(LoginMethodType.Email,
                Email.Create(KevinEmail).Value.Value, cancellationToken) is not null)
            throw new DevelopmentSeedScopeChangedException();

        var culture = references.Cultures.Single(item => item.IsEnabled && item.IsDefault);
        var country = references.Countries.Single(item => item.Code == culture.CountryCode);
        if (string.IsNullOrWhiteSpace(country.DefaultTimeZoneId)
            || string.IsNullOrWhiteSpace(country.DefaultCurrencyCode))
            throw new InvalidOperationException("The default culture country needs a time zone and currency.");

        var anaId = await FindOrCreateUserAsync("Ana", candidate.AnaEmail,
            culture.Code, country.DefaultTimeZoneId, cancellationToken);
        var kevinId = await FindOrCreateUserAsync("Kevin", Email.Create(KevinEmail).Value,
            culture.Code, country.DefaultTimeZoneId, cancellationToken);
        if (candidate.Tenant.Activate().IsFailure)
            throw new InvalidOperationException("The sample organization could not be activated.");

        spaces.Stage(candidate.Tenant, TenantSettings.Create(culture.Code,
            country.DefaultTimeZoneId, country.DefaultCurrencyCode), [anaId, kevinId]);
    }

    internal async Task<DevelopmentPersonalCandidate?> PreparePersonalAsync(DevelopmentPerson person,
        CancellationToken cancellationToken)
    {
        var (name, email) = Person(person);
        var method = await lookup.FindMethodAsync(LoginMethodType.Email, email.Value, cancellationToken);
        if (method is { VerifiedAtUtc: null })
            throw new InvalidOperationException("A sample account email exists without verification.");
        if (method is not null && await HasPersonalAsync(method.UserId, cancellationToken)) return null;

        var draft = await personalSpaces.PrepareAsync(null, null, cancellationToken);
        return new DevelopmentPersonalCandidate(draft, name, email);
    }

    internal async Task StagePersonalAsync(DevelopmentPersonalCandidate candidate,
        CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        var method = await lookup.FindMethodAsync(LoginMethodType.Email,
            candidate.Email.Value, cancellationToken);
        if (method is { VerifiedAtUtc: null })
            throw new InvalidOperationException("A sample account email exists without verification.");
        var userId = method?.UserId ?? await FindOrCreateUserAsync(candidate.Name, candidate.Email,
            candidate.Draft.Culture, candidate.Draft.TimeZoneId, cancellationToken);
        await personalSpaceLock.LockAsync(userId, cancellationToken);
        if ((await accesses.ListForUserAsync(userId, cancellationToken))
            .Any(access => access.Kind == TenantKind.Personal))
            throw new DevelopmentSeedScopeChangedException();
        personalSpaces.Stage(candidate.Draft, userId);
    }

    private async Task<Tenant?> FindBusinessAsync(CancellationToken cancellationToken)
    {
        var kevin = await lookup.FindMethodAsync(LoginMethodType.Email,
            Email.Create(KevinEmail).Value.Value, cancellationToken);
        if (kevin is null) return null;

        var access = (await accesses.ListForUserAsync(kevin.UserId, cancellationToken))
            .Where(row => row.Kind == TenantKind.Business && row.Name == BusinessName)
            .OrderBy(row => row.TenantId)
            .FirstOrDefault();
        return access is null ? null : await context.Tenants.AsNoTracking()
            .SingleAsync(tenant => tenant.Id == access.TenantId, cancellationToken);
    }

    private async Task<bool> HasPersonalAsync(Guid userId, CancellationToken cancellationToken)
    {
        return (await accesses.ListForUserAsync(userId, cancellationToken))
            .Any(access => access.Kind == TenantKind.Personal);
    }

    private async Task<Guid> FindOrCreateUserAsync(string name, Email email, string culture,
        string timeZoneId, CancellationToken cancellationToken)
    {
        var method = await lookup.FindMethodAsync(LoginMethodType.Email, email.Value, cancellationToken);
        if (method is not null)
        {
            if (method.VerifiedAtUtc is null)
                throw new InvalidOperationException("A sample account email already exists without verification.");
            return method.UserId;
        }

        var userId = (await users.CreateAsync(name, culture, timeZoneId, cancellationToken)).Id;
        var loginMethod = LoginMethod.CreateEmail(userId, email);
        loginMethod.Verify(timeProvider.GetUtcNow().UtcDateTime);
        if (loginMethod.MakePrimary().IsFailure)
            throw new InvalidOperationException("The sample email could not become primary.");
        methods.Add(loginMethod);
        await users.SetPrimaryEmailAsync(userId, email, cancellationToken);
        return userId;
    }

    private static (string Name, Email Email) Person(DevelopmentPerson person) => person switch
    {
        DevelopmentPerson.Kevin => ("Kevin", Email.Create(KevinEmail).Value),
        DevelopmentPerson.Carla => ("Carla", Email.Create(CarlaEmail).Value),
        _ => throw new ArgumentOutOfRangeException(nameof(person)),
    };
}

internal sealed record DevelopmentSeedCandidate(Tenant Tenant, Email AnaEmail);

internal sealed record DevelopmentPersonalCandidate(PersonalSpaceDraft Draft, string Name, Email Email);

internal enum DevelopmentPerson { Kevin, Carla }

internal sealed class DevelopmentSeedScopeChangedException : Exception;
