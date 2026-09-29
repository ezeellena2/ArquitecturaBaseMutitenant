using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;

/// <summary>Datos de muestra de Development; nunca reescribe identidades ya creadas.</summary>
internal sealed class DevelopmentSeeder(
    ApplicationDbContext context,
    UserManager<ApplicationUser> users,
    IConfiguration configuration,
    TimeProvider timeProvider)
{
    private const string BusinessName = "Empresa A";
    private const string DefaultAnaEmail = "ana@example.test";
    private const string KevinEmail = "kevin@empresa-a.com";

    internal async Task<DevelopmentSeedCandidate> PrepareAsync(CancellationToken cancellationToken)
    {
        var anaEmail = Email.Create(configuration["Seed:Development:AnaEmail"] ?? DefaultAnaEmail);
        if (anaEmail.IsFailure)
        {
            throw new InvalidOperationException("Seed:Development:AnaEmail must be a valid email address.");
        }

        var existing = await context.Tenants.AsNoTracking().SingleOrDefaultAsync(tenant =>
            tenant.Kind == TenantKind.Business && tenant.Name == BusinessName,
            cancellationToken);
        return new DevelopmentSeedCandidate(existing ?? Tenant.CreateBusiness(BusinessName, requiresApproval: false),
            anaEmail.Value);
    }

    internal async Task SeedAsync(DevelopmentSeedCandidate candidate, ReferenceDataSeedSnapshot references,
        CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        var existing = await context.Tenants.AsNoTracking().SingleOrDefaultAsync(tenant =>
            tenant.Kind == TenantKind.Business && tenant.Name == BusinessName,
            cancellationToken);
        if (existing is not null)
        {
            if (existing.Id != candidate.Tenant.Id)
            {
                throw new DevelopmentSeedScopeChangedException();
            }

            return;
        }

        var culture = references.Cultures.Single(item => item.IsEnabled && item.IsDefault);
        var country = references.Countries.Single(item => item.Code == culture.CountryCode);
        if (string.IsNullOrWhiteSpace(country.DefaultTimeZoneId)
            || string.IsNullOrWhiteSpace(country.DefaultCurrencyCode))
        {
            throw new InvalidOperationException("The default culture country needs a time zone and currency.");
        }

        var business = candidate.Tenant;
        if (business.Activate().IsFailure)
        {
            throw new InvalidOperationException("The sample organization could not be activated.");
        }

        context.Tenants.Add(business);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        await AddMemberAsync("Ana", candidate.AnaEmail, culture.Code, country.DefaultTimeZoneId,
            nowUtc);
        await AddMemberAsync("Kevin", Email.Create(KevinEmail).Value, culture.Code,
            country.DefaultTimeZoneId, nowUtc);
        context.TenantSettings.Add(TenantSettings.Create(culture.Code, country.DefaultTimeZoneId,
            country.DefaultCurrencyCode));
    }

    private async Task AddMemberAsync(string name, Email email, string culture, string timeZoneId,
        DateTime nowUtc)
    {
        var created = ApplicationUser.Create(name, culture, timeZoneId);
        if (created.IsFailure)
        {
            throw new InvalidOperationException("The sample account display name is invalid.");
        }

        var user = created.Value;
        user.Email = email.Value;
        user.EmailConfirmed = true;
        var result = await users.CreateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not create sample account: {string.Join(", ", result.Errors.Select(error => error.Code))}.");
        }

        var method = LoginMethod.CreateEmail(user.Id, email);
        method.Verify(nowUtc);
        if (method.MakePrimary().IsFailure)
        {
            throw new InvalidOperationException("The sample email could not become primary.");
        }

        context.LoginMethods.Add(method);
        var member = Member.Invite(user.Id);
        if (member.Activate(nowUtc).IsFailure)
        {
            throw new InvalidOperationException("The sample membership could not be activated.");
        }

        context.Members.Add(member);
    }
}

internal sealed record DevelopmentSeedCandidate(Tenant Tenant, Email AnaEmail);

internal sealed class DevelopmentSeedScopeChangedException : Exception;
