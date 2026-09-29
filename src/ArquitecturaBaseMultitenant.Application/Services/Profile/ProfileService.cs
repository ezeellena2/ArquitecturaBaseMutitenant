using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Profile;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Profile;

internal sealed class ProfileService(ICurrentUser currentUser, ITenantContext tenantContext,
    IUserRepository users, IUserTenantAccessReader accesses, ITenantSettingsReader settings,
    ICultureCatalog cultures, ICountryCatalog countries, IRequestValidator validator,
    IUnitOfWork unitOfWork, TimeProvider timeProvider, ILogger<ProfileService> logger) : IProfileService
{
    public Task<Result<MeResponse>> GetAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync<MeResponse>(logger, timeProvider, "GetProfile", async () =>
        {
            if (currentUser.UserId is not { } userId || currentUser.Access is not { } access)
            {
                return UserErrors.NotFound;
            }

            var account = await users.GetByIdAsync(userId, cancellationToken);
            if (account is null)
            {
                return UserErrors.NotFound;
            }

            var memberships = await accesses.ListForUserAsync(userId, cancellationToken);
            var organizations = memberships.Where(row => row.Kind == TenantKind.Business)
                .Select(row => new OrganizationSummary(row.TenantId, row.Name, row.Slug,
                    row.TenantStatus, null, row.MemberStatus)).ToArray();
            var personalSpace = memberships.Any(row => row.Kind == TenantKind.Personal);
            var activeMembership = memberships.SingleOrDefault(row => row.TenantId == tenantContext.TenantId);
            var mayReadSettings = activeMembership is
            {
                TenantStatus: TenantStatus.Active,
                MemberStatus: MemberStatus.Active,
            };
            var tenantSettings = mayReadSettings
                ? await settings.FindCurrentAsync(cancellationToken) : null;
            var culture = await cultures.FindAsync(account.Culture, cancellationToken);
            var country = culture is null
                ? null : await countries.FindAsync(culture.CountryCode, cancellationToken);
            var currencyCode = tenantSettings?.DefaultCurrency ?? country?.DefaultCurrencyCode;

            return new MeResponse(account.Id, account.DisplayName, account.PrimaryEmail, access,
                tenantContext.TenantId, personalSpace, organizations, EffectivePermissions.Empty,
                account.Culture, account.TimeZoneId, currencyCode, []);
        });

    public Task<Result> UpdateAsync(UpdateMeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return OperationLog.RunAsync(logger, timeProvider, "UpdateProfile", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid)
            {
                return invalid;
            }

            return await unitOfWork.ExecuteInTransactionAsync(
                ct => UpdateCoreAsync(request, ct), CommitPolicy.OnSuccess, cancellationToken);
        });
    }

    private async Task<Result> UpdateCoreAsync(UpdateMeRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId || await users.GetByIdAsync(userId, cancellationToken) is null)
        {
            return UserErrors.NotFound;
        }

        await users.UpdateProfileAsync(userId, request.DisplayName, request.Culture!, request.TimeZoneId!,
            cancellationToken);
        return Result.Success();
    }
}
