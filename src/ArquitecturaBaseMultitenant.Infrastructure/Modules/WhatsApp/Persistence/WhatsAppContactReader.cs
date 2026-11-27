using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Models.StructuredData;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

internal sealed class WhatsAppContactReader(ApplicationDbContext context, ITenantContext tenant) : IWhatsAppContactReader
{
    // Las columnas con un dato de la tabla de contactos (264); el consentimiento, por su circuito.
    private static readonly SortMap<WhatsAppCustomer> Sort = new()
    {
        ["name"] = customer => customer.Name,
        ["number"] = customer => customer.WaId,
        ["consent"] = customer => customer.Consent,
        ["lastConversationAtUtc"] = customer => customer.LastConversationAtUtc,
    };

    private static readonly SortDescriptor DefaultSort = new("name", Descending: false);

    // Las columnas de las conversaciones de la ficha (264): el equipo por su nombre, sin equipo al final; sin orden pedido, la
    // más nueva primero. El último mensaje (contenido cifrado) y quien la atiende (un nombre de la cuenta) no ordenan.
    private static readonly SortMap<ConversationSortRow> ConversationSort = new()
    {
        ["startedAtUtc"] = row => row.StartedAtUtc,
        ["team"] = row => row.TeamName,
        ["messageCount"] = row => row.MessageCount,
    };

    private static readonly SortDescriptor DefaultConversationSort = new("startedAtUtc", Descending: true);

    public async Task<PagedResult<WhatsAppContactResponse>> ListAsync(WhatsAppContactListRequest request, CancellationToken ct)
    {
        var customers = Customers();
        if (!string.IsNullOrWhiteSpace(request.Tag)) { var tag = request.Tag.Trim(); customers = customers.Where(customer => customer.Tags.Contains(tag)); }
        if (request.Consent is { } consent) customers = customers.Where(customer => customer.Consent == consent);
        var page = await Project(customers.ApplySearch(request.Search, customer => customer.Name, customer => customer.WaId)
                .ApplySort(SortDescriptor.Parse(request.Sort), Sort, DefaultSort, customer => customer.Id))
            .ToPagedResultAsync(request, ct);
        return new PagedResult<WhatsAppContactResponse>([.. page.Items.Select(Response)], page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<WhatsAppContactResponse?> FindAsync(Guid id, CancellationToken ct) =>
        await Project(Customers().Where(customer => customer.Id == id)).SingleOrDefaultAsync(ct) is { } row ? Response(row) : null;

    public async Task<WhatsAppContactCounts> CountAsync(CancellationToken ct) =>
        new(await Customers().CountAsync(ct), await Customers().CountAsync(customer => customer.Consent == WhatsAppConsent.Accepted, ct));

    public Task<PagedResult<WhatsAppContactConversationRow>> ListConversationsAsync(Guid contactId, WhatsAppContactConversationListRequest request,
        CancellationToken ct)
    {
        var tenantId = tenant.RequiredTenantId;
        return (from chat in context.Set<WhatsAppChat>().AsNoTracking().Where(chat => chat.TenantId == tenantId && chat.CustomerId == contactId)
                from team in context.Set<WhatsAppTeam>().AsNoTracking().Where(team => team.TenantId == tenantId && team.Id == chat.TeamId).DefaultIfEmpty()
                from member in context.Members.AsNoTracking()
                    .Where(member => member.TenantId == tenantId && member.UserId == chat.AssigneeUserId && member.Status == MemberStatus.Active)
                    .DefaultIfEmpty()
                select new ConversationSortRow
                {
                    Id = chat.Id,
                    StartedAtUtc = chat.StartedAtUtc,
                    TeamId = chat.TeamId,
                    TeamName = team == null ? null : team.Name,
                    AssigneeUserId = chat.AssigneeUserId,
                    AssigneeMemberId = member == null ? null : member.Id,
                    MessageCount = context.Set<WhatsAppChatMessage>().Count(message => message.TenantId == tenantId && message.ChatId == chat.Id),
                    LastMessagePreview = chat.LastMessagePreview,
                })
            .ApplySort(SortDescriptor.Parse(request.Sort), ConversationSort, DefaultConversationSort, row => row.Id)
            .Select(row => new WhatsAppContactConversationRow(row.Id, row.StartedAtUtc, row.TeamId, row.TeamName, row.AssigneeUserId, row.AssigneeMemberId,
                row.MessageCount, row.LastMessagePreview))
            .ToPagedResultAsync(request, ct);
    }

    public Task<PagedResult<string>> ListTagsAsync(PagedRequest request, CancellationToken ct) =>
        Customers().SelectMany(customer => customer.Tags).Distinct().ApplySearch(request.Search, tag => tag).OrderBy(tag => tag)
            .ToPagedResultAsync(request, ct);

    public async Task<IReadOnlyList<WhatsAppAudienceMember>> ListAudienceAsync(string? tag, CancellationToken ct)
    {
        var customers = Customers().Where(customer => customer.Consent == WhatsAppConsent.Accepted);
        if (!string.IsNullOrWhiteSpace(tag)) { var value = tag.Trim(); customers = customers.Where(customer => customer.Tags.Contains(value)); }
        return await customers.OrderBy(customer => customer.Id).Select(customer => new WhatsAppAudienceMember(customer.Id, customer.WaId)).ToArrayAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, WhatsAppConsent>> ListConsentsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        ids.Count == 0 ? new Dictionary<Guid, WhatsAppConsent>()
            : await Customers().Where(customer => ids.Contains(customer.Id)).ToDictionaryAsync(customer => customer.Id, customer => customer.Consent, ct);

    public async Task<IReadOnlySet<string>> ListExistingAsync(IReadOnlyCollection<string> waIds, CancellationToken ct)
    {
        if (waIds.Count == 0) return new HashSet<string>(StringComparer.Ordinal);
        return (await Customers().Where(customer => waIds.Contains(customer.WaId)).Select(customer => customer.WaId).ToArrayAsync(ct))
            .ToHashSet(StringComparer.Ordinal);
    }

    public async Task<StructuredContactPage> ListStructuredAsync(StructuredContactQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(query.Limit, 1);
        using var timeout = ShortQueryTimeout.Apply(context);
        var customers = Named();
        foreach (var condition in query.Tags)
        {
            // La etiqueta se compara sin distinguir mayúsculas, con los valores como parámetros.
            var tags = condition.Tags.Select(tag => tag.Trim().ToLowerInvariant()).ToArray();
            // ToLower() se traduce a lower() de PostgreSQL, como ApplySearch: la cultura del proceso no interviene.
#pragma warning disable CA1304, CA1311, CA1862
            customers = condition.Include
                ? customers.Where(customer => customer.Tags.Any(tag => tags.Contains(tag.ToLower())))
                : customers.Where(customer => !customer.Tags.Any(tag => tags.Contains(tag.ToLower())));
#pragma warning restore CA1304, CA1311, CA1862
        }

        if (query.ExcludedContactId is { } excluded) customers = customers.Where(customer => customer.Id != excluded);
        var ordered = query.Descending
            ? customers.OrderByDescending(customer => customer.Name).ThenByDescending(customer => customer.Id)
            : customers.OrderBy(customer => customer.Name).ThenBy(customer => customer.Id);
        var rows = await ordered.Take(query.Limit + 1)
            .Select(customer => new StructuredRow { Id = customer.Id, Name = customer.Name!, Tags = customer.Tags }).ToListAsync(ct);
        var exceedsLimit = rows.Count > query.Limit;
        if (exceedsLimit) rows.RemoveAt(rows.Count - 1);
        var total = exceedsLimit ? await customers.CountAsync(ct) : rows.Count;
        return new StructuredContactPage([.. rows.Select(Structured)], total, exceedsLimit);
    }

    public async Task<StructuredContact?> FindStructuredAsync(Guid id, CancellationToken ct) =>
        await Named().Where(customer => customer.Id == id)
            .Select(customer => new StructuredRow { Id = customer.Id, Name = customer.Name!, Tags = customer.Tags })
            .SingleOrDefaultAsync(ct) is { } row ? Structured(row) : null;

    public async Task<PagedResult<StructuredContact>> ListStructuredPageAsync(PagedRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var customers = Named().ApplySearch(request.Search, customer => customer.Name);
        var page = await (SortDescriptor.Parse(request.Sort)?.Descending == true
                ? customers.OrderByDescending(customer => customer.Name).ThenByDescending(customer => customer.Id)
                : customers.OrderBy(customer => customer.Name).ThenBy(customer => customer.Id))
            .Select(customer => new StructuredRow { Id = customer.Id, Name = customer.Name!, Tags = customer.Tags })
            .ToPagedResultAsync(request, ct);
        return new PagedResult<StructuredContact>([.. page.Items.Select(Structured)], page.Page, page.PageSize, page.TotalCount);
    }

    public Task<PagedResult<StructuredContactTag>> ListTagCountsAsync(PagedRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tags = Named().SelectMany(customer => customer.Tags).ApplySearch(request.Search, tag => tag).GroupBy(tag => tag);
        return (SortDescriptor.Parse(request.Sort)?.Descending == true ? tags.OrderByDescending(group => group.Key) : tags.OrderBy(group => group.Key))
            .Select(group => new StructuredContactTag(group.Key, group.Count())).ToPagedResultAsync(request, ct);
    }

    /// <summary>Los contactos que se ofrecen en una lista de otra área: los que tienen nombre (C14 de la E12).</summary>
    private IQueryable<WhatsAppCustomer> Named() =>
        Customers().Where(customer => customer.Name != null && customer.Name != string.Empty);

    private static StructuredContact Structured(StructuredRow row) =>
        new(row.Id, row.Name, [.. row.Tags.Order(StringComparer.OrdinalIgnoreCase)]);

    private IQueryable<WhatsAppCustomer> Customers()
    {
        var tenantId = tenant.RequiredTenantId;
        return context.Set<WhatsAppCustomer>().AsNoTracking().Where(customer => customer.TenantId == tenantId);
    }

    private IQueryable<ContactProjection> Project(IQueryable<WhatsAppCustomer> customers) => customers.Select(customer => new ContactProjection
    {
        Id = customer.Id,
        Name = customer.Name,
        WaId = customer.WaId,
        Tags = customer.Tags,
        Consent = customer.Consent,
        ConsentSource = customer.ConsentSource,
        ConsentChangedAtUtc = customer.ConsentChangedAtUtc,
        LastConversationAtUtc = customer.LastConversationAtUtc,
        Version = customer.Version,
        Linked = context.Set<WhatsAppConversationLink>().Any(link => context.Set<WhatsAppChat>()
            .Any(chat => chat.TenantId == customer.TenantId && chat.CustomerId == customer.Id && chat.Id == link.ChatId)),
        LatestConversationId = context.Set<WhatsAppChat>().Where(chat => chat.TenantId == customer.TenantId && chat.CustomerId == customer.Id)
            .OrderByDescending(chat => chat.LastMessageAtUtc).ThenByDescending(chat => chat.Id).Select(chat => (Guid?)chat.Id).FirstOrDefault(),
    });

    private static WhatsAppContactResponse Response(ContactProjection row) => new(row.Id, row.Name, row.WaId, [.. row.Tags], row.Consent,
        row.ConsentSource, row.ConsentChangedAtUtc, row.Linked, row.LastConversationAtUtc, row.Version, row.LatestConversationId);

    private sealed class ContactProjection
    {
        public Guid Id { get; init; }
        public string? Name { get; init; }
        public string WaId { get; init; } = string.Empty;
        public IReadOnlyCollection<string> Tags { get; init; } = [];
        public WhatsAppConsent Consent { get; init; }
        public WhatsAppConsentSource? ConsentSource { get; init; }
        public DateTime? ConsentChangedAtUtc { get; init; }
        public DateTime? LastConversationAtUtc { get; init; }
        public uint Version { get; init; }
        public Guid? LatestConversationId { get; init; }
        public bool Linked { get; init; }
    }

    private sealed class StructuredRow
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public IReadOnlyCollection<string> Tags { get; init; } = [];
    }

    private sealed class ConversationSortRow
    {
        public Guid Id { get; init; }
        public DateTime StartedAtUtc { get; init; }
        public Guid? TeamId { get; init; }
        public string? TeamName { get; init; }
        public Guid? AssigneeUserId { get; init; }
        public Guid? AssigneeMemberId { get; init; }
        public int MessageCount { get; init; }
        public string? LastMessagePreview { get; init; }
    }
}
