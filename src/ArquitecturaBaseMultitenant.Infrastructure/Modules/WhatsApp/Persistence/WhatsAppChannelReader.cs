using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Time;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

/// <summary>Proyecta los números de la organización del claim y el de Plataforma; el contador sale del mes en la zona de cada número.</summary>
internal sealed class WhatsAppChannelReader(ApplicationDbContext context, ITenantContext tenant, ITimeZoneService zones) : IWhatsAppChannelReader
{
    public async Task<PagedResult<WhatsAppNumberResponse>> ListOrganizationAsync(WhatsAppNumberListRequest request, DateTime nowUtc,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var page = await Organization()
            .ApplySearch(request.Search, value => value.DisplayNumber, value => value.VerifiedName)
            .OrderBy(value => value.DisplayNumber).ThenBy(value => value.Id)
            .ToPagedResultAsync(request, ct);
        return new PagedResult<WhatsAppNumberResponse>(await ProjectAsync(page.Items, nowUtc, ct), page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<WhatsAppNumberResponse?> FindOrganizationAsync(Guid id, DateTime nowUtc, CancellationToken ct)
    {
        var row = await Organization().SingleOrDefaultAsync(value => value.Id == id, ct);
        return row is null ? null : (await ProjectAsync([row], nowUtc, ct))[0];
    }

    public Task<int> CountOrganizationAsync(WhatsAppNumberStatus? status, CancellationToken ct) =>
        Organization().CountAsync(value => status == null || value.Status == status, ct);

    public async Task<IReadOnlyList<WhatsAppNumberPendingRow>> ListPaymentMissingAsync(int limit, CancellationToken ct) =>
        await Organization().Where(value => value.Status == WhatsAppNumberStatus.PaymentMissing)
            .OrderBy(value => value.ConnectedAtUtc).ThenBy(value => value.Id).Take(limit)
            .Select(value => new WhatsAppNumberPendingRow(value.Id, value.DisplayNumber ?? string.Empty, value.ModifiedAtUtc ?? value.ConnectedAtUtc))
            .ToArrayAsync(ct);

    public async Task<WhatsAppPlatformNumberRow?> FindPlatformAsync(DateTime nowUtc, CancellationToken ct)
    {
        var row = await Active().SingleOrDefaultAsync(value => value.OrganizationId == null, ct);
        if (row is null) return null;
        var usage = await UsageAsync([row], nowUtc, ct);
        return new WhatsAppPlatformNumberRow(Project(row, null, usage.GetValueOrDefault(row.Id)), row.ConsultationsEnabled,
            usage.GetValueOrDefault(row.Id)?.OtherDelivered ?? 0);
    }

    public Task<WhatsAppPlatformChannelRow?> FindPlatformStateAsync(CancellationToken ct) =>
        Active().Where(value => value.OrganizationId == null)
            .Select(value => new WhatsAppPlatformChannelRow(value.Id, value.PhoneNumberId, value.DisplayNumber, value.Status, value.ConsultationsEnabled))
            .SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Guid>> ListOrganizationIdsAsync(CancellationToken ct) =>
        await Active().Where(value => value.OrganizationId != null).Select(value => value.OrganizationId!.Value).Distinct().OrderBy(value => value)
            .ToArrayAsync(ct);

    public Task<bool> IsActiveAsync(string phoneNumberId, CancellationToken ct) =>
        Active().AnyAsync(value => value.PhoneNumberId == phoneNumberId, ct);

    public Task<WhatsAppInboundChannel?> FindInboundAsync(Guid? channelId, string phoneNumberId, CancellationToken ct) =>
        (channelId is { } id
            ? context.Set<WhatsAppChannel>().AsNoTracking().Where(value => value.Id == id)
            : Active().Where(value => value.PhoneNumberId == phoneNumberId && value.OrganizationId == null))
        .Select(value => new WhatsAppInboundChannel(value.Id, value.OrganizationId, value.PhoneNumberId, value.Status, value.ConsultationsEnabled))
        .SingleOrDefaultAsync(ct);

    public Task<WhatsAppSendingChannelRow?> FindSendingAsync(Guid? channelId, string phoneNumberId, CancellationToken ct) =>
        (channelId is { } id
            ? context.Set<WhatsAppChannel>().AsNoTracking().Where(value => value.Id == id)
            : Active().Where(value => value.PhoneNumberId == phoneNumberId))
        .Select(value => new WhatsAppSendingChannelRow(value.Id, value.PhoneNumberId, value.WabaId, value.OrganizationId, value.Status, value.EncryptedToken))
        .SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyList<WhatsAppTemplateAccountRow>> ListTemplateAccountsAsync(bool platform, CancellationToken ct) =>
        Accounts(await (platform ? Active().Where(value => value.OrganizationId == null) : Organization()).OrderBy(value => value.ConnectedAtUtc)
            .ThenBy(value => value.Id).Select(value => new WhatsAppTemplateAccountRow(value.Id, value.WabaId, value.EncryptedToken)).ToArrayAsync(ct));

    public async Task<IReadOnlyList<WhatsAppTemplateAccountRow>> ListAllTemplateAccountsAsync(CancellationToken ct) =>
        Accounts(await Active().OrderBy(value => value.ConnectedAtUtc).ThenBy(value => value.Id)
            .Select(value => new WhatsAppTemplateAccountRow(value.Id, value.WabaId, value.EncryptedToken)).ToArrayAsync(ct));

    public Task<bool> HasActiveAccountAsync(string wabaId, CancellationToken ct) => Active().AnyAsync(value => value.WabaId == wabaId, ct);

    private static WhatsAppTemplateAccountRow[] Accounts(WhatsAppTemplateAccountRow[] rows) =>
        [.. rows.DistinctBy(row => row.WabaId, StringComparer.Ordinal)];

    private IQueryable<WhatsAppChannel> Active() =>
        context.Set<WhatsAppChannel>().AsNoTracking().Where(value => value.Status != WhatsAppNumberStatus.Disconnected);

    private IQueryable<WhatsAppChannel> Organization()
    {
        var organizationId = tenant.RequiredTenantId;
        return Active().Where(value => value.OrganizationId == organizationId);
    }

    private async Task<IReadOnlyList<WhatsAppNumberResponse>> ProjectAsync(IReadOnlyList<WhatsAppChannel> rows, DateTime nowUtc, CancellationToken ct)
    {
        if (rows.Count == 0) return [];
        var organizationId = tenant.RequiredTenantId;
        var companyIds = rows.Select(value => value.CompanyId).OfType<Guid>().Distinct().ToArray();
        var companies = companyIds.Length == 0 ? new Dictionary<Guid, string>()
            : await context.Companies.AsNoTracking().Where(company => company.TenantId == organizationId && companyIds.Contains(company.Id))
                .ToDictionaryAsync(company => company.Id, company => company.Name, ct);
        var usage = await UsageAsync(rows, nowUtc, ct);
        return [.. rows.Select(row => Project(row, row.CompanyId is { } company && companies.TryGetValue(company, out var name)
            ? new WhatsAppReferenceResponse(company, name) : null, usage.GetValueOrDefault(row.Id)))];
    }

    private async Task<Dictionary<Guid, WhatsAppChannelUsage>> UsageAsync(IReadOnlyList<WhatsAppChannel> rows, DateTime nowUtc, CancellationToken ct)
    {
        var months = rows.ToDictionary(row => row.Id, row => WhatsAppChannel.UsageMonth(zones.ConvertToLocal(nowUtc, row.UsageTimeZoneId)));
        var ids = months.Keys.ToArray();
        var candidates = months.Values.Distinct().ToArray();
        var usages = await context.Set<WhatsAppChannelUsage>().AsNoTracking()
            .Where(value => ids.Contains(value.ChannelId) && candidates.Contains(value.Month)).ToArrayAsync(ct);
        return usages.Where(value => months[value.ChannelId] == value.Month).ToDictionary(value => value.ChannelId);
    }

    private static WhatsAppNumberResponse Project(WhatsAppChannel row, WhatsAppReferenceResponse? company, WhatsAppChannelUsage? usage) =>
        new(row.Id, row.DisplayNumber ?? string.Empty, row.VerifiedName, company, row.Status, row.Coexistence,
            usage?.ServiceDelivered ?? 0, WhatsAppFeature.FreeServiceMessagesPerMonth, usage?.ServiceCharged ?? 0, row.ConnectedAtUtc, row.Version);

}
