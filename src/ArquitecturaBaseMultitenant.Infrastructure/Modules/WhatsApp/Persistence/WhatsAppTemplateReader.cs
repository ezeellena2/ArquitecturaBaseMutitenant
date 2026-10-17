using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

/// <summary>
/// Las plantillas con lo que Meta dice de ellas en las cuentas de los números vigentes del dueño (organización o Plataforma).
/// El estado se resume en la consulta: rechazada, pausada, aprobada en todas o pendiente.
/// </summary>
internal sealed class WhatsAppTemplateReader(ApplicationDbContext context, ITenantContext tenant) : IWhatsAppTemplateReader
{
    public async Task<PagedResult<WhatsAppTemplateRow>> ListAsync(bool platform, WhatsAppTemplateListRequest request, CancellationToken ct)
    {
        var rows = Project(platform);
        if (request.Status is { } status) rows = rows.Where(row => row.Status == status);
        var page = await rows.ApplySearch(request.Search, row => row.Name).OrderBy(row => row.Name).ThenBy(row => row.Id).ToPagedResultAsync(request, ct);
        return new PagedResult<WhatsAppTemplateRow>([.. page.Items.Select(Row)], page.Page, page.PageSize, page.TotalCount);
    }

    public Task<int> CountAsync(bool platform, WhatsAppTemplateStatus? status, CancellationToken ct) =>
        Project(platform).CountAsync(row => status == null || row.Status == status, ct);

    public async Task<WhatsAppTemplateRow?> FindAsync(bool platform, Guid id, CancellationToken ct) =>
        await Project(platform).SingleOrDefaultAsync(row => row.Id == id, ct) is { } row ? Row(row) : null;

    public async Task<PagedResult<WhatsAppTemplateRow>> ListApprovedAsync(bool platform, string? wabaId,
        IReadOnlyCollection<WhatsAppTemplateCategory> categories, PagedRequest request, CancellationToken ct)
    {
        var rows = Project(platform).Where(row => categories.Contains(row.Category));
        if (wabaId is null) rows = rows.Where(row => row.Status == WhatsAppTemplateStatus.Approved);
        else
        {
            var approved = context.Set<WhatsAppTemplateState>().AsNoTracking()
                .Where(state => state.WabaId == wabaId && state.Status == WhatsAppTemplateStatus.Approved);
            rows = rows.Where(row => approved.Any(state => state.MetaName == row.MetaName && state.MetaLanguage == row.MetaLanguage));
        }
        var page = await rows.ApplySearch(request.Search, row => row.Name).OrderBy(row => row.Name).ThenBy(row => row.Id).ToPagedResultAsync(request, ct);
        return new PagedResult<WhatsAppTemplateRow>([.. page.Items.Select(Row)], page.Page, page.PageSize, page.TotalCount);
    }

    public Task<bool> IsApprovedInAsync(string wabaId, string metaName, string metaLanguage, CancellationToken ct) =>
        context.Set<WhatsAppTemplateState>().AsNoTracking().AnyAsync(state => state.WabaId == wabaId && state.MetaName == metaName
            && state.MetaLanguage == metaLanguage && state.Status == WhatsAppTemplateStatus.Approved, ct);

    public Task<bool> NameTakenAsync(bool platform, string metaName, CancellationToken ct) => platform
        ? context.Set<PlatformWhatsAppTemplate>().AsNoTracking().AnyAsync(value => value.MetaName == metaName, ct)
        : Organization().AnyAsync(value => value.MetaName == metaName, ct);

    public async Task<WhatsAppPlatformTemplateStates> ListPlatformStatesAsync(IReadOnlyCollection<string> metaNames, CancellationToken ct)
    {
        var connectedAtUtc = await Channels(platform: true).OrderBy(channel => channel.ConnectedAtUtc).Select(channel => (DateTime?)channel.ConnectedAtUtc)
            .FirstOrDefaultAsync(ct);
        var accounts = Channels(platform: true).Select(channel => channel.WabaId);
        var states = await context.Set<WhatsAppTemplateState>().AsNoTracking()
            .Where(state => accounts.Contains(state.WabaId) && metaNames.Contains(state.MetaName))
            .OrderBy(state => state.MetaName).ThenBy(state => state.MetaLanguage).ThenBy(state => state.WabaId)
            .Select(state => new WhatsAppTemplateStateRow(state.MetaName, state.MetaLanguage, state.Status, state.Category, state.RejectionReason, state.UpdatedAtUtc))
            .ToArrayAsync(ct);
        return new WhatsAppPlatformTemplateStates(connectedAtUtc, states);
    }

    private IQueryable<TemplateProjection> Project(bool platform)
    {
        var accounts = Channels(platform).Select(channel => channel.WabaId);
        var states = context.Set<WhatsAppTemplateState>().AsNoTracking().Where(state => accounts.Contains(state.WabaId));
        return platform ? Project(context.Set<PlatformWhatsAppTemplate>().AsNoTracking(), states) : Project(Organization(), states);
    }

    private static IQueryable<TemplateProjection> Project<T>(IQueryable<T> templates, IQueryable<WhatsAppTemplateState> states) where T : WhatsAppTemplateBase =>
        templates.Select(template => new TemplateProjection
        {
            Id = template.Id, Name = template.Name, MetaName = template.MetaName, Language = template.Language, MetaLanguage = template.MetaLanguage,
            Category = states.Where(state => state.MetaName == template.MetaName && state.MetaLanguage == template.MetaLanguage)
                .OrderBy(state => state.WabaId).Select(state => (WhatsAppTemplateCategory?)state.Category).FirstOrDefault() ?? template.Category,
            Body = template.Body, Variables = EF.Property<List<WhatsAppTemplateVariable>>(template, "_variables"),
            Status = states.Any(state => state.MetaName == template.MetaName && state.MetaLanguage == template.MetaLanguage
                    && state.Status == WhatsAppTemplateStatus.Rejected) ? WhatsAppTemplateStatus.Rejected
                : states.Any(state => state.MetaName == template.MetaName && state.MetaLanguage == template.MetaLanguage
                    && state.Status == WhatsAppTemplateStatus.Paused) ? WhatsAppTemplateStatus.Paused
                : states.Any(state => state.MetaName == template.MetaName && state.MetaLanguage == template.MetaLanguage)
                    && !states.Any(state => state.MetaName == template.MetaName && state.MetaLanguage == template.MetaLanguage
                        && state.Status != WhatsAppTemplateStatus.Approved) ? WhatsAppTemplateStatus.Approved
                : WhatsAppTemplateStatus.Pending,
            RejectionReason = states.Where(state => state.MetaName == template.MetaName && state.MetaLanguage == template.MetaLanguage
                    && state.Status == WhatsAppTemplateStatus.Rejected).OrderBy(state => state.WabaId).Select(state => state.RejectionReason).FirstOrDefault(),
            ReviewedAtUtc = states.Where(state => state.MetaName == template.MetaName && state.MetaLanguage == template.MetaLanguage)
                .Max(state => (DateTime?)state.UpdatedAtUtc),
            ChangedAtUtc = template.ModifiedAtUtc ?? template.CreatedAtUtc,
            Version = template.Version,
        });

    private IQueryable<WhatsAppTemplate> Organization()
    {
        var tenantId = tenant.RequiredTenantId;
        return context.Set<WhatsAppTemplate>().AsNoTracking().Where(value => value.TenantId == tenantId);
    }

    private IQueryable<WhatsAppChannel> Channels(bool platform)
    {
        var active = context.Set<WhatsAppChannel>().AsNoTracking().Where(channel => channel.Status != WhatsAppNumberStatus.Disconnected);
        if (platform) return active.Where(channel => channel.OrganizationId == null);
        var tenantId = tenant.RequiredTenantId;
        return active.Where(channel => channel.OrganizationId == tenantId);
    }

    private static WhatsAppTemplateRow Row(TemplateProjection row) => new(row.Id, row.Name, row.MetaName, row.Language, row.MetaLanguage, row.Category,
        row.Body, [.. row.Variables], row.Status, row.RejectionReason,
        row.ReviewedAtUtc is { } reviewed && reviewed > row.ChangedAtUtc ? reviewed : row.ChangedAtUtc, row.Version);

    private sealed class TemplateProjection
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string MetaName { get; init; } = string.Empty;
        public string Language { get; init; } = string.Empty;
        public string MetaLanguage { get; init; } = string.Empty;
        public WhatsAppTemplateCategory Category { get; init; }
        public string Body { get; init; } = string.Empty;
        public List<WhatsAppTemplateVariable> Variables { get; init; } = [];
        public WhatsAppTemplateStatus Status { get; init; }
        public string? RejectionReason { get; init; }
        public DateTime? ReviewedAtUtc { get; init; }
        public DateTime ChangedAtUtc { get; init; }
        public uint Version { get; init; }
    }
}
