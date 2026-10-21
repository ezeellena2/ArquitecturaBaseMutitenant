using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>Los operadores que pueden integrar un equipo de Plataforma o recibir una consulta: cuentas con permisos de operador.</summary>
internal sealed class PlatformWhatsAppOperatorResolver(IPlatformAccountReader accounts, LoginMethodAvailabilityPolicy availability,
    IPlatformPermissionAssignmentReader assignments, TimeProvider clock)
{
    internal async Task<PagedResult<WhatsAppReferenceResponse>> ListOptionsAsync(PagedRequest request, CancellationToken ct)
    {
        var page = await accounts.ListAsync(new ListPlatformAccountsRequest
        {
            Page = request.Page,
            PageSize = request.PageSize,
            Search = request.Search,
            Kind = PlatformAccountKind.Operator,
            State = PlatformAccountState.Active,
        }, availability.SignInTypes(), clock.GetUtcNow().UtcDateTime, ct);
        return new PagedResult<WhatsAppReferenceResponse>([.. page.Items.Select(account => new WhatsAppReferenceResponse(account.Id,
            Label(account.DisplayName, account.PrimaryEmail)))], page.Page, page.PageSize, page.TotalCount);
    }

    internal static string Label(string? displayName, Email? email) =>
        string.IsNullOrWhiteSpace(displayName) ? email?.DisplayValue ?? string.Empty : displayName;

    internal async Task<bool> AreOperatorsAsync(IEnumerable<Guid> accountIds, CancellationToken ct)
    {
        foreach (var accountId in accountIds.Distinct())
            if (await assignments.FindPermissionsAsync(accountId, ct) is null) return false;
        return true;
    }
}
