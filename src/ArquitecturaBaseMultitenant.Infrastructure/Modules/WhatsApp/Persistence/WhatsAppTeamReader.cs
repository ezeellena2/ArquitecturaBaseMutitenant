using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

internal sealed class WhatsAppTeamReader(ApplicationDbContext context, ITenantContext tenant) : IWhatsAppTeamReader
{
    // Las columnas con un dato de la tabla de equipos (264): la bandeja por su circuito, los integrantes por su cantidad y el
    // aviso por sus minutos (sin aviso, al final). En Plataforma todos son de clientes y la bandeja no cambia el orden.
    private static readonly SortMap<TeamProjection> Sort = new()
    {
        ["name"] = team => team.Name,
        ["inbox"] = team => team.Inbox,
        ["memberCount"] = team => team.MemberCount,
        ["notice"] = team => team.NotifyAfterMinutes,
    };

    private static readonly SortDescriptor DefaultSort = new("name", Descending: false);

    public async Task<PagedResult<WhatsAppTeamRow>> ListOrganizationAsync(WhatsAppInbox? inbox, PagedRequest request, CancellationToken ct)
    {
        var tenantId = tenant.RequiredTenantId;
        var teams = context.Set<WhatsAppTeam>().AsNoTracking().Where(value => value.TenantId == tenantId);
        if (inbox is { } value) teams = teams.Where(team => team.Inbox == value);
        return Page(await teams.ApplySearch(request.Search, team => team.Name)
            .Select(team => new TeamProjection
            {
                Id = team.Id, Name = team.Name, Inbox = team.Inbox, MemberIds = team.MemberIds, MemberCount = team.MemberIds.Count,
                NotifyAfterMinutes = team.NotifyAfterMinutes, Version = team.Version,
            })
            .ApplySort(SortDescriptor.Parse(request.Sort), Sort, DefaultSort, team => team.Id)
            .ToPagedResultAsync(request, ct));
    }

    public Task<WhatsAppTeamRow?> FindOrganizationAsync(Guid id, CancellationToken ct)
    {
        var tenantId = tenant.RequiredTenantId;
        return context.Set<WhatsAppTeam>().AsNoTracking().Where(team => team.TenantId == tenantId && team.Id == id)
            .Select(team => new WhatsAppTeamRow(team.Id, team.Name, team.Inbox, team.MemberIds, team.NotifyAfterMinutes, team.Version))
            .SingleOrDefaultAsync(ct);
    }

    public async Task<PagedResult<WhatsAppTeamRow>> ListPlatformAsync(PagedRequest request, CancellationToken ct) =>
        Page(await context.Set<PlatformWhatsAppTeam>().AsNoTracking().ApplySearch(request.Search, team => team.Name)
            .Select(team => new TeamProjection
            {
                Id = team.Id, Name = team.Name, Inbox = WhatsAppInbox.Customers, MemberIds = team.MemberIds, MemberCount = team.MemberIds.Count,
                NotifyAfterMinutes = team.NotifyAfterMinutes, Version = team.Version,
            })
            .ApplySort(SortDescriptor.Parse(request.Sort), Sort, DefaultSort, team => team.Id)
            .ToPagedResultAsync(request, ct));

    public Task<WhatsAppTeamRow?> FindPlatformAsync(Guid id, CancellationToken ct) =>
        context.Set<PlatformWhatsAppTeam>().AsNoTracking().Where(team => team.Id == id)
            .Select(team => new WhatsAppTeamRow(team.Id, team.Name, WhatsAppInbox.Customers, team.MemberIds, team.NotifyAfterMinutes, team.Version))
            .SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyList<WhatsAppTeamRow>> ListNotifyingAsync(bool platform, CancellationToken ct)
    {
        if (platform)
            return await context.Set<PlatformWhatsAppTeam>().AsNoTracking().Where(team => team.NotifyAfterMinutes != null).OrderBy(team => team.Id)
                .Select(team => new WhatsAppTeamRow(team.Id, team.Name, WhatsAppInbox.Customers, team.MemberIds, team.NotifyAfterMinutes, team.Version))
                .ToArrayAsync(ct);
        var tenantId = tenant.RequiredTenantId;
        return await context.Set<WhatsAppTeam>().AsNoTracking().Where(team => team.TenantId == tenantId && team.NotifyAfterMinutes != null)
            .OrderBy(team => team.Id)
            .Select(team => new WhatsAppTeamRow(team.Id, team.Name, team.Inbox, team.MemberIds, team.NotifyAfterMinutes, team.Version))
            .ToArrayAsync(ct);
    }

    public async Task<IReadOnlyList<WhatsAppTeamMemberRow>> ListActiveMembersAsync(IReadOnlyCollection<Guid> memberIds, CancellationToken ct)
    {
        if (memberIds.Count == 0) return [];
        var tenantId = tenant.RequiredTenantId;
        return await context.Members.AsNoTracking()
            .Where(member => member.TenantId == tenantId && memberIds.Contains(member.Id) && member.Status == MemberStatus.Active && member.UserId != null)
            .Select(member => new WhatsAppTeamMemberRow(member.Id, member.UserId!.Value)).ToArrayAsync(ct);
    }

    public async Task<IReadOnlyList<WhatsAppTeamMemberRow>> ListMembersOfAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0) return [];
        var tenantId = tenant.RequiredTenantId;
        return await context.Members.AsNoTracking()
            .Where(member => member.TenantId == tenantId && member.UserId != null && userIds.Contains(member.UserId.Value)
                && member.Status == MemberStatus.Active)
            .Select(member => new WhatsAppTeamMemberRow(member.Id, member.UserId!.Value)).ToArrayAsync(ct);
    }

    private static PagedResult<WhatsAppTeamRow> Page(PagedResult<TeamProjection> page) => new([.. page.Items.Select(team =>
        new WhatsAppTeamRow(team.Id, team.Name, team.Inbox, team.MemberIds, team.NotifyAfterMinutes, team.Version))], page.Page, page.PageSize, page.TotalCount);

    private sealed class TeamProjection
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public WhatsAppInbox Inbox { get; init; }
        public IReadOnlyList<Guid> MemberIds { get; init; } = [];
        public int MemberCount { get; init; }
        public int? NotifyAfterMinutes { get; init; }
        public uint Version { get; init; }
    }
}
