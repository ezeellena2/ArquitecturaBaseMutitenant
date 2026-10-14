using System.Globalization;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// Arma las bandejas para quien las mira: nombres de quien tomó y de los empleados, «Mío», el número que la recibió y los
/// conteos (CC15: la bandeja entera; 0 en la que la persona no puede ver). En Plataforma, la cuenta del número va solo a
/// quien puede ver cuentas.
/// </summary>
internal sealed class WhatsAppConversationResponseBuilder(IWhatsAppConversationReader reader, IUserDisplayNameReader names, ICurrentUser user,
    IPermissionService permissions, IPlatformPermissionService platformPermissions, IUserLookup accounts, TimeProvider clock)
{
    internal async Task<WhatsAppConversationListResponse> ListAsync(WhatsAppInbox? inbox, WhatsAppConversationListRequest request, CancellationToken ct)
    {
        var page = await reader.ListAsync(inbox, request, Me, ct);
        return new(new PagedResult<WhatsAppConversationRowResponse>(await RowsAsync(page.Items, ct), page.Page, page.PageSize, page.TotalCount),
            await CountsAsync(inbox, ct));
    }

    internal async Task<WhatsAppConversationRowResponse?> RowAsync(WhatsAppInbox? inbox, Guid id, CancellationToken ct) =>
        await reader.FindAsync(inbox, id, ct) is { } row ? (await RowsAsync([row], ct))[0] : null;

    /// <summary>La conversación y sus mensajes: la página 1 trae los más recientes (CC15).</summary>
    internal async Task<WhatsAppConversationResponse?> ConversationAsync(WhatsAppInbox? inbox, Guid id, PagedRequest request, CancellationToken ct) =>
        await RowAsync(inbox, id, ct) is { } row ? new(row, await reader.ListMessagesAsync(inbox, id, request, ct)) : null;

    /// <summary>
    /// Las personas a las que se puede pasar el chat, con los que ya tienen tomados en la bandeja. Son pocas (las de los
    /// equipos, hasta 200 por equipo): se buscan y paginan acá, sin distinguir acentos ni mayúsculas.
    /// </summary>
    internal async Task<PagedResult<WhatsAppTransferPersonResponse>> PeopleAsync(WhatsAppInbox? inbox,
        IReadOnlyDictionary<Guid, WhatsAppTeamResponseBuilder.TeamPerson> people, PagedRequest request, CancellationToken ct)
    {
        var taken = await reader.CountTakenAsync(inbox, [.. people.Values.Select(person => person.UserId).Distinct()], ct);
        var search = request.Search?.Trim();
        var matches = people.Where(pair => string.IsNullOrEmpty(search) || CultureInfo.InvariantCulture.CompareInfo.IndexOf(pair.Value.Name, search,
                CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0)
            .OrderBy(pair => pair.Value.Name, StringComparer.CurrentCulture).ThenBy(pair => pair.Key).ToArray();
        return new PagedResult<WhatsAppTransferPersonResponse>([.. matches.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(pair => new WhatsAppTransferPersonResponse(pair.Key, pair.Value.Name, taken.GetValueOrDefault(pair.Value.UserId)))],
            request.Page, request.PageSize, matches.Length);
    }

    /// <summary>Los sin tomar de cada bandeja que la persona puede ver; null en la otra (CC09).</summary>
    internal async Task<WhatsAppUntakenResponse> UntakenAsync(CancellationToken ct) => new(
        await permissions.HasPermissionAsync(Me, Permissions.WhatsApp.Customers, ct) ? await reader.CountAsync(WhatsAppInbox.Customers, true, ct) : null,
        await permissions.HasPermissionAsync(Me, Permissions.WhatsApp.Internal, ct) ? await reader.CountAsync(WhatsAppInbox.Internal, true, ct) : null);

    /// <summary>El nombre de una cuenta (quien tomó el chat o quien escribe); null si la cuenta ya no lo muestra.</summary>
    internal async Task<string?> NameAsync(Guid userId, CancellationToken ct) => (await names.FindAsync([userId], ct)).GetValueOrDefault(userId);

    private Guid Me => user.UserId ?? throw new InvalidOperationException("An authenticated user is required.");

    private async Task<WhatsAppConversationCountsResponse> CountsAsync(WhatsAppInbox? inbox, CancellationToken ct)
    {
        if (inbox is not { } box) return new(await reader.CountAsync(null, false, ct), 0, await reader.CountAsync(null, true, ct));
        var customers = await permissions.HasPermissionAsync(Me, Permissions.WhatsApp.Customers, ct)
            ? await reader.CountAsync(WhatsAppInbox.Customers, false, ct) : 0;
        var internals = await permissions.HasPermissionAsync(Me, Permissions.WhatsApp.Internal, ct)
            ? await reader.CountAsync(WhatsAppInbox.Internal, false, ct) : 0;
        return new(customers, internals, await reader.CountAsync(box, true, ct));
    }

    private async Task<IReadOnlyList<WhatsAppConversationRowResponse>> RowsAsync(IReadOnlyList<WhatsAppConversationRow> rows, CancellationToken ct)
    {
        var people = await names.FindAsync([.. rows.SelectMany(row => new[] { row.AssigneeUserId, row.MemberUserId }).OfType<Guid>().Distinct()], ct);
        var showAccounts = rows.Any(row => row.Inbox is null) && await platformPermissions.HasPermissionAsync(Me, PlatformPermissions.Accounts.Read, ct);
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var result = new List<WhatsAppConversationRowResponse>(rows.Count);
        foreach (var row in rows)
        {
            var accountId = showAccounts ? await accounts.FindVerifiedUserIdAsync(LoginMethodType.Phone, row.WaId, ct) : null;
            var assignee = row.AssigneeUserId is { } taker
                ? new WhatsAppReferenceResponse(row.Inbox is null ? taker : row.AssigneeMemberId ?? taker, people.GetValueOrDefault(taker) ?? string.Empty)
                : null;
            var contactName = row.MemberUserId is { } member ? people.GetValueOrDefault(member) : row.ContactName;
            var window = row.LastInboundAtUtc + WhatsAppChatBase.Window;
            result.Add(new WhatsAppConversationRowResponse(row.Id, row.Inbox, row.ContactId, contactName, row.WaId, row.Tags,
                row.LastMessagePreview, row.LastMessageAtUtc, row.AwaitingReply, row.TeamId is { } team ? new(team, row.TeamName ?? string.Empty) : null,
                assignee, LinkedToAccount: false, window > nowUtc ? window : null, row.ChannelStatus == WhatsAppNumberStatus.Disconnected, accountId,
                row.Version, new WhatsAppConversationChannelResponse(row.ChannelId, row.ChannelNumber ?? string.Empty, row.ChannelStatus),
                row.AssigneeUserId == Me));
        }
        return result;
    }
}
