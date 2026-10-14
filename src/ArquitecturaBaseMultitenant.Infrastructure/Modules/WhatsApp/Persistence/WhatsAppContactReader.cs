using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

internal sealed class WhatsAppContactReader(ApplicationDbContext context, ITenantContext tenant) : IWhatsAppContactReader
{
    public async Task<PagedResult<WhatsAppContactResponse>> ListAsync(WhatsAppContactListRequest request, CancellationToken ct)
    {
        var customers = Customers();
        if (!string.IsNullOrWhiteSpace(request.Tag)) { var tag = request.Tag.Trim(); customers = customers.Where(customer => customer.Tags.Contains(tag)); }
        if (request.Consent is { } consent) customers = customers.Where(customer => customer.Consent == consent);
        var page = await Project(customers.ApplySearch(request.Search, customer => customer.Name, customer => customer.WaId)
                .OrderBy(customer => customer.Name == null).ThenBy(customer => customer.Name).ThenBy(customer => customer.Id))
            .ToPagedResultAsync(request, ct);
        return new PagedResult<WhatsAppContactResponse>([.. page.Items.Select(Response)], page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<WhatsAppContactResponse?> FindAsync(Guid id, CancellationToken ct) =>
        await Project(Customers().Where(customer => customer.Id == id)).SingleOrDefaultAsync(ct) is { } row ? Response(row) : null;

    public async Task<WhatsAppContactCounts> CountAsync(CancellationToken ct) =>
        new(await Customers().CountAsync(ct), await Customers().CountAsync(customer => customer.Consent == WhatsAppConsent.Accepted, ct));

    public Task<PagedResult<WhatsAppContactConversationRow>> ListConversationsAsync(Guid contactId, PagedRequest request, CancellationToken ct)
    {
        var tenantId = tenant.RequiredTenantId;
        return (from chat in context.Set<WhatsAppChat>().AsNoTracking().Where(chat => chat.TenantId == tenantId && chat.CustomerId == contactId)
                from team in context.Set<WhatsAppTeam>().AsNoTracking().Where(team => team.TenantId == tenantId && team.Id == chat.TeamId).DefaultIfEmpty()
                from member in context.Members.AsNoTracking()
                    .Where(member => member.TenantId == tenantId && member.UserId == chat.AssigneeUserId && member.Status == MemberStatus.Active)
                    .DefaultIfEmpty()
                orderby chat.LastMessageAtUtc descending, chat.Id descending
                select new WhatsAppContactConversationRow(chat.Id, chat.StartedAtUtc, chat.TeamId, team == null ? null : team.Name, chat.AssigneeUserId,
                    member == null ? null : member.Id,
                    context.Set<WhatsAppChatMessage>().Count(message => message.TenantId == tenantId && message.ChatId == chat.Id),
                    chat.LastMessagePreview))
            .ToPagedResultAsync(request, ct);
    }

    public Task<PagedResult<string>> ListTagsAsync(PagedRequest request, CancellationToken ct) =>
        Customers().SelectMany(customer => customer.Tags).Distinct().ApplySearch(request.Search, tag => tag).OrderBy(tag => tag)
            .ToPagedResultAsync(request, ct);

    public async Task<IReadOnlySet<string>> FindExistingAsync(IReadOnlyCollection<string> waIds, CancellationToken ct)
    {
        if (waIds.Count == 0) return new HashSet<string>(StringComparer.Ordinal);
        return (await Customers().Where(customer => waIds.Contains(customer.WaId)).Select(customer => customer.WaId).ToArrayAsync(ct))
            .ToHashSet(StringComparer.Ordinal);
    }

    private IQueryable<WhatsAppCustomer> Customers()
    {
        var tenantId = tenant.RequiredTenantId;
        return context.Set<WhatsAppCustomer>().AsNoTracking().Where(customer => customer.TenantId == tenantId);
    }

    private IQueryable<ContactProjection> Project(IQueryable<WhatsAppCustomer> customers) => customers.Select(customer => new ContactProjection
    {
        Id = customer.Id, Name = customer.Name, WaId = customer.WaId, Tags = customer.Tags, Consent = customer.Consent,
        ConsentSource = customer.ConsentSource, ConsentChangedAtUtc = customer.ConsentChangedAtUtc,
        LastConversationAtUtc = customer.LastConversationAtUtc, Version = customer.Version,
        LatestConversationId = context.Set<WhatsAppChat>().Where(chat => chat.TenantId == customer.TenantId && chat.CustomerId == customer.Id)
            .OrderByDescending(chat => chat.LastMessageAtUtc).ThenByDescending(chat => chat.Id).Select(chat => (Guid?)chat.Id).FirstOrDefault(),
    });

    private static WhatsAppContactResponse Response(ContactProjection row) => new(row.Id, row.Name, row.WaId, [.. row.Tags], row.Consent,
        row.ConsentSource, row.ConsentChangedAtUtc, LinkedToAccount: false, row.LastConversationAtUtc, row.Version, row.LatestConversationId);

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
    }
}
