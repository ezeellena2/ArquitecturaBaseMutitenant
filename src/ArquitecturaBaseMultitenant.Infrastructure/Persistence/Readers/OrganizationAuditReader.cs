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
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

internal sealed class OrganizationAuditReader(ApplicationDbContext context, ITenantContext tenantContext)
    : IOrganizationAuditReader
{

    /// <summary>
    /// Las acciones de Plataforma sobre la organización que muestra su auditoría, como el tablero Auditoria-Org, con el
    /// cambio de estado que significan. Salen de los <see cref="SecurityEvent"/> con <c>TargetTenantId</c>: de cada una
    /// se lee solo la fecha y el tipo, nunca el operador, el motivo ni la IP, que son de Plataforma.
    /// </summary>
    private static readonly Dictionary<SecurityEventType, (TenantStatus Before, TenantStatus After)> PlatformActions =
        new() { [SecurityEventType.OrganizationApproved] = (TenantStatus.PendingApproval, TenantStatus.Active) };

    public async Task<CursorResult<OrganizationAuditRow>> ListAsync(ListOrganizationAuditRequest request,
        DateTime? startUtc, DateTime? endUtc, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.RequiredTenantId;
        var entries = await FilteredAsync(request, startUtc, endUtc, cancellationToken);
        if (request.ActorId is { } actorId)
        {
            // «Quién» filtra por persona: el operador de Plataforma no se muestra.
            entries = entries.Where(entry => entry.ActorId == actorId && entry.ActorKind == AuditActorKind.User);
        }

        // «Quién» por tipo: «Sistema», «Plataforma» o las personas.
        if (request.ActorKind is { } actorKindName)
        {
            var actorKind = Enum.Parse<AuditActorKind>(actorKindName);
            entries = entries.Where(entry => entry.ActorKind == actorKind);
        }

        // Las entradas y las acciones de Plataforma van en un mismo cursor, por fecha.
        var feed = entries.Select(entry => new FeedKey
        {
            Id = entry.Id, OccurredAtUtc = entry.OccurredAtUtc, FromPlatform = false,
        });
        if (ShowsPlatformActions(request))
        {
            feed = feed.Concat(PlatformActionsOf(tenantId, startUtc, endUtc).Select(action => new FeedKey
            {
                Id = action.Id, OccurredAtUtc = action.OccurredAtUtc, FromPlatform = true,
            }));
        }

        var page = await feed.ToCursorResultAsync(request, key => key.OccurredAtUtc, key => key.Id, cancellationToken);
        var auditIds = page.Items.Where(key => !key.FromPlatform).Select(key => key.Id).ToArray();
        AuditEntry[] audit = auditIds.Length == 0
            ? []
            : await context.Set<AuditEntry>().AsNoTracking()
                .Where(entry => entry.TenantId == tenantId && auditIds.Contains(entry.Id))
                .ToArrayAsync(cancellationToken);
        var platformIds = page.Items.Where(key => key.FromPlatform).Select(key => key.Id).ToArray();
        var platform = platformIds.Length == 0
            ? new Dictionary<Guid, SecurityEventType>()
            : await context.SecurityEvents.AsNoTracking().Where(action => platformIds.Contains(action.Id))
                .ToDictionaryAsync(action => action.Id, action => action.Type, cancellationToken);
        var rows = await RowsAsync(audit, cancellationToken);
        return new CursorResult<OrganizationAuditRow>([
            .. page.Items.Select(key => key.FromPlatform
                ? PlatformRow(key, tenantId, platform[key.Id])
                : rows[key.Id]),
        ], page.NextCursor, page.HasMore);
    }

    private async Task<Dictionary<Guid, OrganizationAuditRow>> RowsAsync(AuditEntry[] page,
        CancellationToken cancellationToken)
    {
        if (page.Length == 0) return [];
        var actorIds = page.Where(entry => entry.ActorKind == AuditActorKind.User && entry.ActorId != null)
            .Select(entry => entry.ActorId!.Value).Distinct().ToArray();
        var actors = actorIds.Length == 0
            ? new Dictionary<Guid, string?>()
            : await context.Users.AsNoTracking().Where(user => actorIds.Contains(user.Id))
                .Select(user => new { user.Id, user.DisplayName, user.Status })
                .ToDictionaryAsync(user => user.Id, user => user.Status == UserStatus.Deleted
                    ? FormattingTexts.DeletedAccount : user.DisplayName, cancellationToken);
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
        var displayData = await ResolveDisplayDataAsync(page, cancellationToken);
        return page.ToDictionary(entry => entry.Id, entry => new OrganizationAuditRow(entry.Id, entry.OccurredAtUtc,
            entry.Action, entry.EntityType, entry.EntityId, entry.ActorId, entry.ActorKind,
            entry.ActorId is { } id ? actors.GetValueOrDefault(id) : null,
            displayData[entry.Id].AffectedDisplayName, displayData[entry.Id].CompanyName,
            entry.Changes, entry.ActorId is { } emailOwner ? actorEmails.GetValueOrDefault(emailOwner) : null));
    }

    // Sin búsqueda ni persona: la búsqueda mira los cambios y quién actuó, y una acción de Plataforma no muestra ninguno
    // de los dos. «Quién» puede ser «Plataforma». El tema es «Organización».
    private static bool ShowsPlatformActions(ListOrganizationAuditRequest request) =>
        string.IsNullOrWhiteSpace(request.Search) && request.ActorId is null
        && (request.ActorKind is null or nameof(AuditActorKind.PlatformOperator))
        && (request.Category is null || OrganizationAuditCategories.EntityTypes[request.Category].Contains(nameof(Tenant)));

    private IQueryable<SecurityEvent> PlatformActionsOf(Guid tenantId, DateTime? startUtc, DateTime? endUtc)
    {
        SecurityEventType[] types = [.. PlatformActions.Keys];
        var actions = context.SecurityEvents.AsNoTracking()
            .Where(action => action.TargetTenantId == tenantId && types.Contains(action.Type));
        if (startUtc is { } start) actions = actions.Where(action => action.OccurredAtUtc >= start);
        if (endUtc is { } end) actions = actions.Where(action => action.OccurredAtUtc < end);
        return actions;
    }

    /// <summary>
    /// La acción de Plataforma como una entrada de la organización: la organización cambió de estado, por Plataforma,
    /// sin decir qué operador fue.
    /// </summary>
    private static OrganizationAuditRow PlatformRow(FeedKey key, Guid tenantId, SecurityEventType type)
    {
        var (before, after) = PlatformActions[type];
        var changes = JsonSerializer.Serialize(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(Tenant.Status)] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["old"] = before.ToString(),
                ["new"] = after.ToString(),
            },
        });
        return new OrganizationAuditRow(key.Id, key.OccurredAtUtc, AuditAction.Updated, nameof(Tenant), tenantId,
            null, AuditActorKind.PlatformOperator, null, null, null, changes);
    }

    private async Task<Dictionary<Guid, AuditDisplayData>> ResolveDisplayDataAsync(
        AuditEntry[] entries, CancellationToken cancellationToken)
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
                .Select(user => new { user.Id, user.DisplayName, user.Status })
                .ToDictionaryAsync(user => user.Id, user => user.Status == UserStatus.Deleted
                    ? FormattingTexts.DeletedAccount : user.DisplayName, cancellationToken);
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

        // Sin nombre ni invitación, por su correo verificado (primero el principal), enmascarado, como lo muestra
        // Usuarios: por ejemplo, quien registró la organización y todavía no puso su nombre.
        var unnamedAccounts = members.Where(member => member.UserId.HasValue
                && string.IsNullOrWhiteSpace(memberNames[member.Id]))
            .ToDictionary(member => member.Id, member => member.UserId!.Value);
        if (unnamedAccounts.Count > 0)
        {
            var accountIds = unnamedAccounts.Values.Distinct().ToArray();
            var emails = (await context.LoginMethods.AsNoTracking()
                    .Where(method => accountIds.Contains(method.UserId) && method.Type == LoginMethodType.Email
                        && method.VerifiedAtUtc != null)
                    .OrderByDescending(method => method.IsPrimary).ThenBy(method => method.Id)
                    .Select(method => new { method.UserId, method.Value }).ToArrayAsync(cancellationToken))
                .GroupBy(method => method.UserId).ToDictionary(group => group.Key, group => group.First().Value);
            foreach (var (memberId, accountId) in unnamedAccounts)
                if (emails.TryGetValue(accountId, out var email)) memberNames[memberId] = PersonalDataMask.Email(email);
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

        var result = new Dictionary<Guid, AuditDisplayData>(entries.Length);
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
        DateTime? startUtc, DateTime? endUtc, CancellationToken cancellationToken) =>
        (await ListActorsPagedAsync(request, new ListOrganizationAuditActorsRequest(), startUtc, endUtc, cancellationToken)).Items;

    public async Task<PagedResult<OrganizationAuditActorRow>> ListActorsPagedAsync(ListOrganizationAuditRequest filters,
        ListOrganizationAuditActorsRequest page, DateTime? startUtc, DateTime? endUtc, CancellationToken cancellationToken)
    {
        var unfiltered = filters with { ActorId = null, ActorKind = null };
        var keys = (await FilteredAsync(unfiltered, startUtc, endUtc, cancellationToken))
            .Select(entry => new { ActorId = entry.ActorKind == AuditActorKind.User ? entry.ActorId : null,
                Order = entry.ActorKind == AuditActorKind.User ? 0 : entry.ActorKind == AuditActorKind.System ? 1 : 2 });
        if (ShowsPlatformActions(unfiltered))
            keys = keys.Concat(PlatformActionsOf(tenantContext.RequiredTenantId, startUtc, endUtc)
                .Select(action => new { ActorId = (Guid?)null, Order = 2 }));
        keys = keys.Distinct();
        var deletedAccount = FormattingTexts.DeletedAccount;
        var system = AuditTexts.Actor(AuditActorKind.System);
        var platform = AuditTexts.Actor(AuditActorKind.PlatformOperator);
        var options = from actor in keys
                      join user in context.Users.AsNoTracking() on actor.ActorId equals (Guid?)user.Id into identities
                      from user in identities.DefaultIfEmpty()
                      select new ActorOption
                      {
                          ActorId = actor.ActorId, Order = actor.Order,
                          DisplayName = actor.Order != 0 ? actor.Order == 1 ? system : platform
                              : user == null ? null : user.Status == UserStatus.Deleted ? deletedAccount : user.DisplayName,
                      };
        options = options.ApplySearch(page.Search, actor => actor.DisplayName);
        // El orden de grupos es fijo (personas, Sistema, Plataforma); el orden pedido cambia sólo sus nombres.
        var grouped = options.OrderBy(actor => actor.Order);
        var ordered = SortDescriptor.Parse(page.Sort)?.Descending == true
            ? grouped.ThenByDescending(actor => actor.DisplayName) : grouped.ThenBy(actor => actor.DisplayName);
        var result = await ordered.ThenBy(actor => actor.ActorId).ToPagedResultAsync(page, cancellationToken);
        return new PagedResult<OrganizationAuditActorRow>([.. result.Items.Select(actor => new OrganizationAuditActorRow(
            actor.ActorId, actor.Order == 0 ? AuditActorKind.User : actor.Order == 1 ? AuditActorKind.System : AuditActorKind.PlatformOperator,
            actor.Order == 0 ? actor.DisplayName : null))],
            result.Page, result.PageSize, result.TotalCount);
    }

    private sealed class ActorOption
    {
        public Guid? ActorId { get; init; }
        public int Order { get; init; }
        public string? DisplayName { get; init; }
    }

    private async Task<IQueryable<AuditEntry>> FilteredAsync(ListOrganizationAuditRequest request,
        DateTime? startUtc, DateTime? endUtc, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.RequiredTenantId;
        IQueryable<AuditEntry> entries;
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var matchingActorIds = await context.Users.AsNoTracking().ApplySearch(request.Search, user => user.DisplayName)
                .Select(user => user.Id).ToArrayAsync(cancellationToken);
            // La expresión de ApplySearch, con sus índices trigram (IX_AuditEntries_Search_*): "Changes" es jsonb y
            // ApplySearch no lo pasa a texto. Lo controla SearchIndexTests.
            var pattern = $"%{EscapeLike(request.Search.Trim())}%";
            entries = context.Set<AuditEntry>().FromSqlInterpolated($"""
                SELECT * FROM tenant."AuditEntries"
                WHERE "TenantId" = {tenantId}
                  AND (public.f_unaccent(lower("EntityType")) LIKE public.f_unaccent(lower({pattern})) ESCAPE E'\\'
                    OR public.f_unaccent(lower("Changes"::text)) LIKE public.f_unaccent(lower({pattern})) ESCAPE E'\\'
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
            var entityTypes = OrganizationAuditCategories.EntityTypes[category];
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

    /// <summary>Una entrada o una acción de Plataforma en el cursor de la auditoría.</summary>
    private sealed class FeedKey
    {
        public Guid Id { get; init; }
        public DateTime OccurredAtUtc { get; init; }
        public bool FromPlatform { get; init; }
    }
}
