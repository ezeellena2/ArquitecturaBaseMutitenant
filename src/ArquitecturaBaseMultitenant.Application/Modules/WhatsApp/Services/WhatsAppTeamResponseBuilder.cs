using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// Arma equipos con los nombres de sus integrantes. En la organización cada integrante es un miembro activo (memberId) y
/// su nombre sale de su cuenta (vacío si no lo tiene, como en Usuarios); en Plataforma, una cuenta de operador. Un integrante
/// que dejó de estar activo no se muestra.
/// </summary>
internal sealed class WhatsAppTeamResponseBuilder(IWhatsAppTeamReader teams, IUserDisplayNameReader names)
{
    internal async Task<IReadOnlyList<WhatsAppTeamResponse>> RowsAsync(IReadOnlyList<WhatsAppTeamRow> rows, bool platform, CancellationToken ct)
    {
        var people = await PeopleAsync(rows.SelectMany(row => row.MemberIds), platform, ct);
        return [.. rows.Select(row =>
        {
            var members = Members(row, people);
            return new WhatsAppTeamResponse(row.Id, row.Name, row.Inbox, members.Count,
                [.. members.Take(WhatsAppFeature.MaxMemberPreview).Select(member => member.Name)], row.NotifyAfterMinutes, row.Version);
        })];
    }

    internal async Task<WhatsAppTeamDetailResponse> DetailAsync(WhatsAppTeamRow row, bool platform, CancellationToken ct) =>
        new(row.Id, row.Name, row.Inbox, Members(row, await PeopleAsync(row.MemberIds, platform, ct)), row.NotifyAfterMinutes, row.Version);

    /// <summary>Integrante → cuenta y nombre; en la organización, solo los miembros activos.</summary>
    internal async Task<IReadOnlyDictionary<Guid, TeamPerson>> PeopleAsync(IEnumerable<Guid> ids, bool platform, CancellationToken ct)
    {
        var distinct = ids.Distinct().ToArray();
        var accounts = platform ? distinct.ToDictionary(id => id, id => id)
            : (await teams.ListActiveMembersAsync(distinct, ct)).ToDictionary(member => member.MemberId, member => member.UserId);
        var displayNames = await names.FindAsync([.. accounts.Values.Distinct()], ct);
        return accounts.ToDictionary(pair => pair.Key, pair => new TeamPerson(pair.Value, displayNames.GetValueOrDefault(pair.Value) ?? string.Empty));
    }

    private static List<WhatsAppReferenceResponse> Members(WhatsAppTeamRow row, IReadOnlyDictionary<Guid, TeamPerson> people) =>
        [.. row.MemberIds.Where(people.ContainsKey).Select(id => new WhatsAppReferenceResponse(id, people[id].Name))
            .OrderBy(member => member.Name, StringComparer.CurrentCulture).ThenBy(member => member.Id)];

    internal sealed record TeamPerson(Guid UserId, string Name);
}
