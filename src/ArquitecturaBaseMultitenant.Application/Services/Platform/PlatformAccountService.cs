using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Common.Text;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

/// <summary>
/// Consulta global de cuentas. El listado y la ficha identifican la cuenta con su correo principal; la ficha muestra los
/// métodos enmascarados, sus membresías desde el índice global y los permisos si es operadora.
/// </summary>
internal sealed class PlatformAccountService(IPlatformAccountReader reader, ILoginMethodReader methods,
    LoginMethodAvailabilityPolicy availability, ITenantReader tenants, ILastTenantAdminGuard soleOwners,
    IRequestValidator validator, TimeProvider clock,
    ILogger<PlatformAccountService> logger) : IPlatformAccountService
{
    public Task<Result<PagedResult<PlatformAccountResponse>>> ListAsync(ListPlatformAccountsRequest request, CancellationToken ct) =>
        OperationLog.RunAsync<PagedResult<PlatformAccountResponse>>(logger, clock, "ListPlatformAccounts", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
            var page = await reader.ListAsync(request, availability.SignInTypes(), clock.GetUtcNow().UtcDateTime, ct);
            return new PagedResult<PlatformAccountResponse>([.. page.Items.Select(ToResponse)], page.Page,
                page.PageSize, page.TotalCount);
        });

    public Task<Result<PlatformAccountFilterCounts>> CountAsync(ListPlatformAccountsRequest request, CancellationToken ct) =>
        OperationLog.RunAsync<PlatformAccountFilterCounts>(logger, clock, "CountPlatformAccounts", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
            return await reader.CountAsync(request, availability.SignInTypes(), clock.GetUtcNow().UtcDateTime, ct);
        });

    public Task<Result<PlatformAccountDetailResponse>> FindAsync(Guid accountId, CancellationToken ct) =>
        OperationLog.RunAsync<PlatformAccountDetailResponse>(logger, clock, "FindPlatformAccount", async () =>
        {
            var row = await reader.FindAsync(accountId, availability.SignInTypes(), clock.GetUtcNow().UtcDateTime, ct);
            if (row is null) return UserErrors.NotFound;
            var account = row.Account;
            var state = row.LastStateChange;
            var deleted = account.State == PlatformAccountState.Deleted;
            var loginMethods = new List<PlatformAccountLoginMethodResponse>();
            var accountMethods = await methods.ListByUserIdAsync(accountId, ct);
            foreach (var method in accountMethods
                .OrderByDescending(method => method.IsPrimary).ThenBy(method => method.Id))
            {
                var organization = method.ManagedByTenantId is { } tenantId ? await tenants.FindByIdAsync(tenantId, ct) : null;
                loginMethods.Add(new PlatformAccountLoginMethodResponse(method.Id, method.Type,
                    PersonalDataMask.LoginMethod(method.Type, method.Value, method.ContactEmail?.Value),
                    method.IsPrimary, method.VerifiedAtUtc, organization?.Name,
                    await availability.IsAvailableAsync(accountId, method, ct)));
            }

            var deletion = !deleted && row.DeletionRequestedAtUtc is { } requested && row.DeletionScheduledForUtc is { } scheduled
                ? new PlatformAccountDeletionResponse(requested, scheduled, row.DeletionRequestedByPlatform) : null;
            return new PlatformAccountDetailResponse(account.Id, DisplayName(account), deleted ? null : account.PrimaryEmail,
                Kind(account), account.State,
                row.CreatedAtUtc, row.DeletedAtUtc, account.LastSignInAtUtc, account.LastSignInMethod, account.InvitationExpiresAtUtc,
                account.HasPersonalSpace, deletion,
                loginMethods, [.. row.Organizations.Select(organization => organization with
                { RoleName = organization.RoleName is null ? null : RoleTexts.Name(organization.RoleName) })], deleted ? [] : await soleOwners.ListSoleOwnedOrganizationsAsync(accountId, ct),
                row.OperatorPermissions, row.OperatorVersion,
                state is null ? null : new PlatformAccountStateChangeResponse(state.AtUtc,
                    AuditTexts.SecurityEvent(state.Type), state.Reason))
            {
                MaskedPrimaryPhone = deleted ? null : accountMethods
                    .Where(method => method.Type == LoginMethodType.Phone && method.IsPrimary && method.VerifiedAtUtc is not null)
                    .Select(method => PersonalDataMask.Phone(method.Value)).SingleOrDefault(),
            };
        });

    private static PlatformAccountResponse ToResponse(PlatformAccountRow row) =>
        new(row.Id, DisplayName(row), row.PrimaryEmail, Kind(row), row.State, row.HasPersonalSpace, row.OrganizationCount, row.LastSignInAtUtc,
            row.LastSignInMethod, row.InvitationExpiresAtUtc)
        {
            MaskedPrimaryPhone = row.State != PlatformAccountState.Deleted && row.PrimaryPhone is { } phone
                ? PersonalDataMask.Phone(phone) : null,
        };

    // La identidad eliminada guarda el nombre en la cultura del worker; se traduce al leer.
    private static string? DisplayName(PlatformAccountRow row) => row.State == PlatformAccountState.Deleted
        ? FormattingTexts.DeletedAccount : row.DisplayName;

    private static PlatformAccountKind Kind(PlatformAccountRow row) =>
        row.IsOperator ? PlatformAccountKind.Operator : PlatformAccountKind.Person;
}
