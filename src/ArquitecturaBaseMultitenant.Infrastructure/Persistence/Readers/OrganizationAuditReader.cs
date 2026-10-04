using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Common.Text;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Auditing;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

internal sealed class OrganizationAuditReader(ApplicationDbContext context, ITenantContext tenantContext)
    : IOrganizationAuditReader
{
    private static readonly Dictionary<string, string[]> Categories = new(StringComparer.Ordinal)
    {
        ["users"] = ["Member", "Invitation"],
        ["roles"] = ["Role", "RoleAssignment"],
        ["companies"] = ["Company", "CompanyMembership"],
        ["settings"] = ["TenantSettings", "TenantDomain"],
        ["organization"] = ["PublicPage", "Tenant"],
    };

    public async Task<CursorResult<OrganizationAuditRow>> ListAsync(ListOrganizationAuditRequest request,
        DateTime? startUtc, DateTime? endUtc, CancellationToken cancellationToken)
    {
        var entries = await FilteredAsync(request, startUtc, endUtc, cancellationToken);
        if (request.ActorId is { } actorId)
        {
            entries = entries.Where(entry => entry.ActorId == actorId);
        }

        var page = await entries.ToCursorResultAsync(request, entry => entry.OccurredAtUtc,
            entry => entry.Id, cancellationToken);
        var actorIds = page.Items.Where(entry => entry.ActorKind == AuditActorKind.User && entry.ActorId != null)
            .Select(entry => entry.ActorId!.Value).Distinct().ToArray();
        var actors = actorIds.Length == 0
            ? new Dictionary<Guid, string?>()
            : await context.Users.AsNoTracking().Where(user => actorIds.Contains(user.Id))
                .Select(user => new { user.Id, user.DisplayName })
                .ToDictionaryAsync(user => user.Id, user => user.DisplayName, cancellationToken);
        // «Quién» del detalle: nombre · correo (el verificado, primero el principal). Quien ya no es miembro de la
        // organización se muestra solo con su nombre (D7).
        var tenantId = tenantContext.RequiredTenantId;
        var memberActorIds = actorIds.Length == 0
            ? []
            : await context.Members.AsNoTracking()
                .Where(member => member.TenantId == tenantId && member.UserId != null
                    && actorIds.Contains(member.UserId.Value)
                    && (member.Status == MemberStatus.Active || member.Status == MemberStatus.Inactive))
                .Select(member => member.UserId!.Value).ToArrayAsync(cancellationToken);
        var actorEmails = memberActorIds.Length == 0
            ? new Dictionary<Guid, Email>()
            : (await context.LoginMethods.AsNoTracking()
                    .Where(method => memberActorIds.Contains(method.UserId) && method.Type == LoginMethodType.Email
                        && method.VerifiedAtUtc != null)
                    .OrderByDescending(method => method.IsPrimary).ThenBy(method => method.Id)
                    .Select(method => new { method.UserId, method.Value }).ToArrayAsync(cancellationToken))
                .GroupBy(method => method.UserId).ToDictionary(group => group.Key, group => Email.Create(group.First().Value).Value);
        var displayData = await ResolveDisplayDataAsync(page.Items, cancellationToken);
        var rows = page.Items.Select(entry => new OrganizationAuditRow(entry.Id, entry.OccurredAtUtc,
            entry.Action, entry.EntityType, entry.EntityId, entry.ActorId, entry.ActorKind,
            entry.ActorId is { } id ? actors.GetValueOrDefault(id) : null,
            displayData[entry.Id].AffectedDisplayName, displayData[entry.Id].CompanyName,
            entry.Changes, entry.ActorId is { } emailOwner ? actorEmails.GetValueOrDefault(emailOwner) : null)).ToArray();
        return new CursorResult<OrganizationAuditRow>(rows, page.NextCursor, page.HasMore);
    }

    private async Task<Dictionary<Guid, AuditDisplayData>> ResolveDisplayDataAsync(
        IReadOnlyCollection<AuditEntry> entries, CancellationToken cancellationToken)
    {
        var invitationIds = entries.Where(entry => entry.EntityType == nameof(Invitation))
            .Select(entry => entry.EntityId).Distinct().ToArray();
        var invitations = invitationIds.Length == 0
            ? new Dictionary<Guid, Invitation>()
            : await context.Invitations.AsNoTracking().Where(invitation => invitationIds.Contains(invitation.Id))
                .ToDictionaryAsync(invitation => invitation.Id, cancellationToken);

        var memberIds = entries.Select(entry => entry.EntityType switch
            {
                nameof(Member) => entry.EntityId,
                nameof(CompanyMembership) or nameof(RoleAssignment) => ReadGuid(entry.Changes, "MemberId"),
                nameof(Invitation) => invitations.GetValueOrDefault(entry.EntityId)?.MemberId ??
                                      ReadGuid(entry.Changes, "MemberId"),
                _ => null,
            })
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        var members = memberIds.Length == 0
            ? []
            : await context.Members.AsNoTracking().Where(member => memberIds.Contains(member.Id))
                .Select(member => new { member.Id, member.UserId }).ToArrayAsync(cancellationToken);
        var userIds = members.Where(member => member.UserId.HasValue).Select(member => member.UserId!.Value)
            .Distinct().ToArray();
        var userNames = userIds.Length == 0
            ? new Dictionary<Guid, string?>()
            : await context.Users.AsNoTracking().Where(user => userIds.Contains(user.Id))
                .Select(user => new { user.Id, user.DisplayName })
                .ToDictionaryAsync(user => user.Id, user => user.DisplayName, cancellationToken);
        var memberNames = members.ToDictionary(member => member.Id,
            member => member.UserId is { } userId ? userNames.GetValueOrDefault(userId) : null);
        // Quien todavía no tiene cuenta ni nombre se nombra por el correo de su última invitación, enmascarado.
        var unnamedMemberIds = memberNames.Where(pair => string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => pair.Key).ToArray();
        if (unnamedMemberIds.Length > 0)
        {
            var destinations = await context.Invitations.AsNoTracking()
                .Where(invitation => unnamedMemberIds.Contains(invitation.MemberId))
                .OrderByDescending(invitation => invitation.IssuedAtUtc)
                .Select(invitation => new { invitation.MemberId, invitation.Destination })
                .ToArrayAsync(cancellationToken);
            foreach (var invited in destinations.GroupBy(invitation => invitation.MemberId))
                memberNames[invited.Key] = PersonalDataMask.Email(invited.First().Destination.DisplayValue);
        }

        var roleIds = entries.Select(entry => entry.EntityType switch
            {
                nameof(Role) => entry.EntityId,
                nameof(RoleAssignment) => ReadGuid(entry.Changes, "RoleId"),
                _ => null,
            })
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        var roles = roleIds.Length == 0
            ? []
            : await context.Roles.AsNoTracking().Where(role => roleIds.Contains(role.Id))
                .Select(role => new { role.Id, role.Name, role.CompanyId }).ToArrayAsync(cancellationToken);
        var roleDetails = roles.ToDictionary(role => role.Id);

        var invitationCompanies = entries.Where(entry => entry.EntityType == nameof(Invitation))
            .ToDictionary(entry => entry.Id, entry => ReadGuids(entry.Changes, AuditChangeKeys.CompanyIds));
        var companyIds = entries.Select(entry => entry.EntityType switch
            {
                nameof(Company) => entry.EntityId,
                nameof(CompanyMembership) or nameof(RoleAssignment) => ReadGuid(entry.Changes, "CompanyId"),
                nameof(Role) => roleDetails.GetValueOrDefault(entry.EntityId)?.CompanyId,
                _ => null,
            })
            .Concat(roleDetails.Values.Where(role => role.CompanyId.HasValue).Select(role => role.CompanyId))
            .Concat(invitationCompanies.Values.SelectMany(ids => ids).Select(id => (Guid?)id))
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        var companyNames = companyIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await context.Companies.AsNoTracking().Where(company => companyIds.Contains(company.Id))
                .Select(company => new { company.Id, company.Name })
                .ToDictionaryAsync(company => company.Id, company => company.Name, cancellationToken);

        var result = new Dictionary<Guid, AuditDisplayData>(entries.Count);
        foreach (var entry in entries)
        {
            var memberId = entry.EntityType switch
            {
                nameof(Member) => entry.EntityId,
                nameof(CompanyMembership) or nameof(RoleAssignment) => ReadGuid(entry.Changes, "MemberId"),
                nameof(Invitation) => invitations.GetValueOrDefault(entry.EntityId)?.MemberId ??
                                      ReadGuid(entry.Changes, "MemberId"),
                _ => null,
            };
            var role = entry.EntityType == nameof(Role)
                ? roleDetails.GetValueOrDefault(entry.EntityId)
                : null;
            var companyId = entry.EntityType switch
            {
                nameof(Company) => entry.EntityId,
                nameof(CompanyMembership) or nameof(RoleAssignment) => ReadGuid(entry.Changes, "CompanyId"),
                nameof(Role) => role?.CompanyId,
                _ => null,
            };
            var affectedName = memberId is { } id ? memberNames.GetValueOrDefault(id) : null;
            if (string.IsNullOrWhiteSpace(affectedName) && entry.EntityType == nameof(Company))
                affectedName = companyNames.GetValueOrDefault(entry.EntityId);
            if (string.IsNullOrWhiteSpace(affectedName) && entry.EntityType == nameof(Role))
                affectedName = role?.Name;
            if (string.IsNullOrWhiteSpace(affectedName) && entry.EntityType == nameof(Invitation) &&
                invitations.TryGetValue(entry.EntityId, out var invitation))
                affectedName = PersonalDataMask.Email(invitation.Destination.DisplayValue);

            // Una invitación a empresas va en esas empresas, por nombre y en orden.
            var companyName = companyId is { } companyGuid
                ? companyNames.GetValueOrDefault(companyGuid)
                : invitationCompanies.GetValueOrDefault(entry.Id) is { Length: > 0 } invited
                    ? string.Join(", ", invited.Select(id => companyNames.GetValueOrDefault(id)).OfType<string>()
                        .Order(StringComparer.CurrentCulture))
                    : null;
            result[entry.Id] = new AuditDisplayData(affectedName, string.IsNullOrEmpty(companyName) ? null : companyName);
        }

        return result;
    }

    public async Task<IReadOnlyList<OrganizationAuditActorRow>> ListActorsAsync(ListOrganizationAuditRequest request,
        DateTime? startUtc, DateTime? endUtc, CancellationToken cancellationToken)
    {
        var actors = await (await FilteredAsync(request with { ActorId = null }, startUtc, endUtc,
                cancellationToken))
            .Select(entry => new { entry.ActorId, entry.ActorKind })
            .Distinct().ToArrayAsync(cancellationToken);
        var actorIds = actors.Where(actor => actor.ActorKind == AuditActorKind.User && actor.ActorId != null)
            .Select(actor => actor.ActorId!.Value).Distinct().ToArray();
        var names = actorIds.Length == 0
            ? new Dictionary<Guid, string?>()
            : await context.Users.AsNoTracking().Where(user => actorIds.Contains(user.Id))
                .Select(user => new { user.Id, user.DisplayName })
                .ToDictionaryAsync(user => user.Id, user => user.DisplayName, cancellationToken);
        return actors.Select(actor => new OrganizationAuditActorRow(actor.ActorId, actor.ActorKind,
                actor.ActorId is { } id ? names.GetValueOrDefault(id) : null))
            .OrderBy(actor => actor.ActorKind)
            .ThenBy(actor => actor.DisplayName, StringComparer.CurrentCulture)
            .ToArray();
    }

    private async Task<IQueryable<AuditEntry>> FilteredAsync(ListOrganizationAuditRequest request,
        DateTime? startUtc, DateTime? endUtc, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.RequiredTenantId;
        IQueryable<AuditEntry> entries;
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{EscapeLike(request.Search.Trim())}%";
            var matchingActorIds = await context.Users.AsNoTracking()
                .Where(user => user.DisplayName != null && EF.Functions.ILike(user.DisplayName, pattern, "\\"))
                .Select(user => user.Id).ToArrayAsync(cancellationToken);
            entries = context.Set<AuditEntry>().FromSqlInterpolated($"""
                SELECT * FROM tenant."AuditEntries"
                WHERE "TenantId" = {tenantId}
                  AND ("EntityType" ILIKE {pattern} ESCAPE E'\\'
                    OR "Changes"::text ILIKE {pattern} ESCAPE E'\\'
                    OR "ActorId" = ANY ({matchingActorIds}))
                """).AsNoTracking();
        }
        else
        {
            entries = context.Set<AuditEntry>().AsNoTracking();
        }

        entries = entries.Where(entry => entry.TenantId == tenantId);
        if (startUtc is { } start) entries = entries.Where(entry => entry.OccurredAtUtc >= start);
        if (endUtc is { } end) entries = entries.Where(entry => entry.OccurredAtUtc < end);
        if (request.Category is { } category)
        {
            var entityTypes = Categories[category];
            entries = entries.Where(entry => entityTypes.Contains(entry.EntityType));
        }

        return entries;
    }

    private static string EscapeLike(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);

    private static Guid? ReadGuid(string changes, string propertyName)
    {
        try
        {
            using var document = JsonDocument.Parse(changes);
            if (!document.RootElement.TryGetProperty(propertyName, out var property)) return null;
            foreach (var side in new[] { "new", "old" })
            {
                if (!property.TryGetProperty(side, out var value) || value.ValueKind != JsonValueKind.String) continue;
                if (Guid.TryParse(value.GetString(), out var id)) return id;
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Guid[] ReadGuids(string changes, string propertyName)
    {
        try
        {
            using var document = JsonDocument.Parse(changes);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty(propertyName, out var property)
                || property.ValueKind != JsonValueKind.Object
                || !property.TryGetProperty("new", out var value) || value.ValueKind != JsonValueKind.Array) return [];
            return
            [
                .. value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => Guid.TryParse(item.GetString(), out var id) ? id : Guid.Empty)
                    .Where(id => id != Guid.Empty),
            ];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed record AuditDisplayData(string? AffectedDisplayName, string? CompanyName);
}
