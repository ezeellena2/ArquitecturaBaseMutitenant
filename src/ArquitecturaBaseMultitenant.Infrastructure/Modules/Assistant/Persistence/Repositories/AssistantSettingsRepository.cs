using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Repositories;

internal sealed class AssistantSettingsRepository(ApplicationDbContext context, ITenantContext tenant) : IAssistantSettingsRepository
{
    public async Task<AssistantPlatformSettings?> GetPlatformAsync(uint expectedVersion, CancellationToken ct)
    {
        context.RequireTransaction();
        await context.AcquireAdvisoryLocksAsync([AssistantLockKeys.PlatformSettings], ct);
        var row = await context.Set<AssistantPlatformSettings>().SingleOrDefaultAsync(ct);
        Check(row?.Version, expectedVersion);
        if (row is not null) context.Entry(row).Property(value => value.Version).OriginalValue = expectedVersion;
        return row;
    }
    public async Task<AssistantOrganizationSettings?> GetOrganizationAsync(uint expectedVersion, CancellationToken ct)
    {
        context.RequireTransaction();
        var tenantId = tenant.RequiredTenantId;
        await context.AcquireAdvisoryLocksAsync([AssistantLockKeys.OrganizationSettings(tenantId),
            AdvisoryLockKeys.For(tenantId, "tenant-admin", "assignments")], ct);
        var row = await context.Set<AssistantOrganizationSettings>().SingleOrDefaultAsync(value => value.TenantId == tenantId, ct);
        Check(row?.Version, expectedVersion);
        if (row is not null) context.Entry(row).Property(value => value.Version).OriginalValue = expectedVersion;
        return row;
    }
    public async Task<AssistantOrganizationPolicy?> GetPolicyAsync(Guid tenantId, uint expectedVersion, CancellationToken ct)
    {
        context.RequireTransaction();
        await context.AcquireAdvisoryLocksAsync([AssistantLockKeys.Policy(tenantId)], ct);
        var row = await context.Set<AssistantOrganizationPolicy>().SingleOrDefaultAsync(value => value.OrganizationId == tenantId, ct);
        Check(row?.Version, expectedVersion);
        if (row is not null) context.Entry(row).Property(value => value.Version).OriginalValue = expectedVersion;
        return row;
    }
    public async Task<uint> LockPlatformVersionAsync(CancellationToken ct)
    {
        context.RequireTransaction();
        await context.AcquireAdvisoryLocksAsync([AssistantLockKeys.PlatformSettings], ct);
        return await context.Set<AssistantPlatformSettings>().AsNoTracking().Select(value => (uint?)value.Version).SingleOrDefaultAsync(ct) ?? 0;
    }
    public async Task<uint> LockOrganizationVersionAsync(CancellationToken ct)
    {
        context.RequireTransaction();
        var tenantId = tenant.RequiredTenantId;
        await context.AcquireAdvisoryLocksAsync([AssistantLockKeys.OrganizationSettings(tenantId)], ct);
        return await context.Set<AssistantOrganizationSettings>().AsNoTracking().Where(value => value.TenantId == tenantId)
            .Select(value => (uint?)value.Version).SingleOrDefaultAsync(ct) ?? 0;
    }
    public Task<AssistantPlatformProviderIssue?> FindPlatformIssueAsync(CancellationToken ct)
    {
        context.RequireTransaction();
        return context.Set<AssistantPlatformProviderIssue>().SingleOrDefaultAsync(ct);
    }
    public Task<AssistantOrganizationProviderIssue?> FindOrganizationIssueAsync(CancellationToken ct)
    {
        context.RequireTransaction();
        var tenantId = tenant.RequiredTenantId;
        return context.Set<AssistantOrganizationProviderIssue>().SingleOrDefaultAsync(value => value.TenantId == tenantId, ct);
    }
    public void Add(AssistantPlatformProviderIssue issue) { context.RequireTransaction(); context.Add(issue); }
    public void Add(AssistantOrganizationProviderIssue issue) { context.RequireTransaction(); _ = tenant.RequiredTenantId; context.Add(issue); }
    public void Add(AssistantPlatformSettings row) { context.RequireTransaction(); context.Add(row); }
    public void Add(AssistantOrganizationSettings row) { context.RequireTransaction(); _ = tenant.RequiredTenantId; context.Add(row); }
    public void Add(AssistantOrganizationPolicy row) { context.RequireTransaction(); context.Add(row); }
    public void MarkUpdated(AssistantPlatformSettings row) { context.RequireTransaction(); context.Entry(row).State = EntityState.Modified; }
    public void MarkUpdated(AssistantOrganizationSettings row) { context.RequireTransaction(); _ = tenant.RequiredTenantId; context.Entry(row).State = EntityState.Modified; }
    public void MarkUpdated(AssistantOrganizationPolicy row) { context.RequireTransaction(); context.Entry(row).State = EntityState.Modified; }
    private static void Check(uint? actual, uint expected)
    {
        if ((actual ?? 0) != expected) throw new ConcurrencyConflictException();
    }
}
