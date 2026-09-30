using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using ArquitecturaBaseMultitenant.Infrastructure.ReferenceData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

/// <summary>Datos de recorrido creados únicamente en la base Testcontainers del test.</summary>
internal static class SampleAccountsFixture
{
    public static async Task SeedAsync(IServiceProvider provider, string firstEmail,
        CancellationToken cancellationToken)
    {
        await provider.SeedDatabaseAsync(cancellationToken);
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var seeder = ActivatorUtilities.CreateInstance<SampleAccountSeeder>(services);
        var references = await ReferenceDataSeedSnapshot.LoadAsync(
            services.GetRequiredService<JsonReferenceDataCatalog>(), cancellationToken);
        var context = services.GetRequiredService<ApplicationDbContext>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var tenantScope = services.GetRequiredService<ITenantScope>();

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var candidate = await seeder.PrepareBusinessAsync(firstEmail, cancellationToken);
            using var entered = tenantScope.Enter(candidate.Tenant.Id);
            try
            {
                await unitOfWork.ExecuteInTransactionAsync(async ct =>
                {
                    await context.AcquireAdvisoryLocksAsync([AdvisoryLockKeys.Seed], ct);
                    await seeder.StageBusinessAsync(candidate, references, ct);
                    return Result.Success();
                }, CommitPolicy.OnSuccess, cancellationToken);
                break;
            }
            catch (SampleSeedScopeChangedException) when (attempt == 0)
            {
                // Otra réplica confirmó primero; el nuevo intento toma su tenant.
            }
        }

        foreach (var person in new[] { SamplePerson.Kevin, SamplePerson.Carla })
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var candidate = await seeder.PreparePersonalAsync(person, cancellationToken);
                if (candidate is null) break;
                using var entered = tenantScope.Enter(candidate.Draft.Tenant.Id);
                try
                {
                    await unitOfWork.ExecuteInTransactionAsync(async ct =>
                    {
                        await context.AcquireAdvisoryLocksAsync([AdvisoryLockKeys.Seed], ct);
                        await seeder.StagePersonalAsync(candidate, ct);
                        return Result.Success();
                    }, CommitPolicy.OnSuccess, cancellationToken);
                    break;
                }
                catch (SampleSeedScopeChangedException) when (attempt == 0)
                {
                    // La cuenta recibió su espacio en la otra réplica.
                }
            }
        }
    }
}

/// <summary>
/// Construye cuentas y espacios de muestra únicamente dentro de la base Testcontainers del test.
/// Reutiliza los provisionadores reales para que los recorridos tengan accesos y membresías coherentes.
/// </summary>
internal sealed class SampleAccountSeeder(
    ApplicationDbContext context,
    IUserLookup lookup,
    IUserRepository users,
    ILoginMethodRepository methods,
    IUserTenantAccessReader accesses,
    IPersonalSpaceLock personalSpaceLock,
    IPersonalSpaceProvisioner personalSpaces,
    TenantSpaceProvisioner spaces,
    ILegalRepository legal,
    TimeProvider timeProvider)
{
    private const string BusinessName = "Empresa A";
    private const string KevinEmail = "kevin@empresa-a.test";
    private const string CarlaEmail = "carla@example.test";

    public async Task<SampleBusinessCandidate> PrepareBusinessAsync(string firstEmail,
        CancellationToken cancellationToken)
    {
        var email = Email.Create(firstEmail);
        if (email.IsFailure) throw new ArgumentException("The test email is invalid.", nameof(firstEmail));
        var existing = await FindBusinessAsync(cancellationToken);
        return new SampleBusinessCandidate(existing ?? Tenant.CreateBusiness(BusinessName,
            requiresApproval: false), email.Value);
    }

    public async Task StageBusinessAsync(SampleBusinessCandidate candidate,
        ReferenceDataSeedSnapshot references, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        if (await context.Tenants.AsNoTracking().AnyAsync(tenant =>
                tenant.Id == candidate.Tenant.Id, cancellationToken)) return;

        if (await lookup.FindMethodAsync(LoginMethodType.Email, KevinEmail, cancellationToken) is not null)
            throw new SampleSeedScopeChangedException();

        var culture = references.Cultures.Single(item => item.IsEnabled && item.IsDefault);
        var country = references.Countries.Single(item => item.Code == culture.CountryCode);
        if (string.IsNullOrWhiteSpace(country.DefaultTimeZoneId)
            || string.IsNullOrWhiteSpace(country.DefaultCurrencyCode))
            throw new InvalidOperationException("The test culture needs a time zone and currency.");

        var firstUserId = await FindOrCreateUserAsync("Ana", candidate.FirstEmail,
            culture.Code, country.DefaultTimeZoneId, cancellationToken);
        var kevinId = await FindOrCreateUserAsync("Kevin", Email.Create(KevinEmail).Value,
            culture.Code, country.DefaultTimeZoneId, cancellationToken);
        if (candidate.Tenant.Activate().IsFailure)
            throw new InvalidOperationException("The sample organization could not be activated.");
        spaces.Stage(candidate.Tenant, TenantSettings.Create(culture.Code,
            country.DefaultTimeZoneId, country.DefaultCurrencyCode), [firstUserId, kevinId]);
    }

    public async Task<SamplePersonalCandidate?> PreparePersonalAsync(SamplePerson person,
        CancellationToken cancellationToken)
    {
        var (name, email) = person switch
        {
            SamplePerson.Kevin => ("Kevin", Email.Create(KevinEmail).Value),
            SamplePerson.Carla => ("Carla", Email.Create(CarlaEmail).Value),
            _ => throw new ArgumentOutOfRangeException(nameof(person)),
        };
        var method = await lookup.FindMethodAsync(LoginMethodType.Email, email.Value, cancellationToken);
        if (method is { VerifiedAtUtc: null })
            throw new InvalidOperationException("The test email exists without verification.");
        if (method is not null && await HasPersonalAsync(method.UserId, cancellationToken)) return null;

        var draft = await personalSpaces.PrepareAsync(null, null, cancellationToken);
        return new SamplePersonalCandidate(draft, name, email);
    }

    public async Task StagePersonalAsync(SamplePersonalCandidate candidate,
        CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        var method = await lookup.FindMethodAsync(LoginMethodType.Email,
            candidate.Email.Value, cancellationToken);
        if (method is { VerifiedAtUtc: null })
            throw new InvalidOperationException("The test email exists without verification.");
        var userId = method?.UserId ?? await FindOrCreateUserAsync(candidate.Name, candidate.Email,
            candidate.Draft.Culture, candidate.Draft.TimeZoneId, cancellationToken);
        await personalSpaceLock.LockAsync(userId, cancellationToken);
        if (await HasPersonalAsync(userId, cancellationToken))
            throw new SampleSeedScopeChangedException();
        personalSpaces.Stage(candidate.Draft, userId);
    }

    private async Task<Tenant?> FindBusinessAsync(CancellationToken cancellationToken)
    {
        var kevin = await lookup.FindMethodAsync(LoginMethodType.Email, KevinEmail, cancellationToken);
        if (kevin is null) return null;

        var access = (await accesses.ListForUserAsync(kevin.UserId, cancellationToken))
            .Where(row => row.Kind == TenantKind.Business && row.Name == BusinessName)
            .OrderBy(row => row.TenantId)
            .FirstOrDefault();
        return access is null ? null : await context.Tenants.AsNoTracking()
            .SingleAsync(tenant => tenant.Id == access.TenantId, cancellationToken);
    }

    private async Task<bool> HasPersonalAsync(Guid userId, CancellationToken cancellationToken) =>
        (await accesses.ListForUserAsync(userId, cancellationToken))
        .Any(access => access.Kind == TenantKind.Personal);

    private async Task<Guid> FindOrCreateUserAsync(string name, Email email, string culture,
        string timeZoneId, CancellationToken cancellationToken)
    {
        var method = await lookup.FindMethodAsync(LoginMethodType.Email, email.Value, cancellationToken);
        if (method is not null)
        {
            if (method.VerifiedAtUtc is null)
                throw new InvalidOperationException("The test email exists without verification.");
            return method.UserId;
        }

        var userId = (await users.CreateAsync(name, culture, timeZoneId, cancellationToken)).Id;
        var loginMethod = LoginMethod.CreateEmail(userId, email);
        loginMethod.Verify(timeProvider.GetUtcNow().UtcDateTime);
        if (loginMethod.MakePrimary().IsFailure)
            throw new InvalidOperationException("The test email could not become primary.");
        methods.Add(loginMethod);
        await users.SetPrimaryEmailAsync(userId, email, cancellationToken);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var kind in new[] { LegalDocumentKind.Terms, LegalDocumentKind.Privacy })
        {
            var document = await legal.GetCurrentDocumentAsync(kind, nowUtc, cancellationToken)
                ?? throw new InvalidOperationException("The test account needs current legal documents.");
            legal.AddAcceptance(LegalAcceptance.Create(userId, document, nowUtc, null, null));
        }
        return userId;
    }
}

internal sealed record SampleBusinessCandidate(Tenant Tenant, Email FirstEmail);
internal sealed record SamplePersonalCandidate(PersonalSpaceDraft Draft, string Name, Email Email);
internal enum SamplePerson { Kevin, Carla }
internal sealed class SampleSeedScopeChangedException : Exception;
