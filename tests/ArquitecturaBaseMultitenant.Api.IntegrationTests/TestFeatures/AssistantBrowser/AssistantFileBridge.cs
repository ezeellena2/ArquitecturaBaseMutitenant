using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Recorrido;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Features;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantBrowser;

/// <summary>Control local sólo del PostgreSQL efímero; no expone endpoints ni toma identidades del modelo.</summary>
public sealed class AssistantFileBridge
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly IServiceProvider _services;
    private readonly string _commandFile;
    private readonly Dictionary<AssistantFinancialBinding, Guid> _fixtures = [];

    public AssistantFileBridge(IServiceProvider services, string readyFile, bool isolated)
    {
        if (!isolated || !services.GetRequiredService<IHostEnvironment>().IsEnvironment("Testing"))
            throw new InvalidOperationException("Assistant bridge requires isolated Testing.");
        RecorridoPaths.Validate(readyFile);
        _services = services;
        _commandFile = Path.ChangeExtension(readyFile, ".assistant.json");
    }

    public async Task<bool> TryExecuteAsync(CancellationToken ct)
    {
        var done = _commandFile + ".done";
        if (!File.Exists(_commandFile) || File.Exists(done)) return false;
        if ((File.GetAttributes(_commandFile) & FileAttributes.ReparsePoint) != 0 || new FileInfo(_commandFile).Length > 2048)
            throw new InvalidOperationException("Assistant bridge requires a bounded regular file.");
        var request = AssistantBrowserRequest.Parse(await File.ReadAllTextAsync(_commandFile, ct));
        object response;
        try { response = new { request.Id, ok = true, data = await ExecuteAsync(request, ct) }; }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        { response = new { request.Id, ok = false, errorCode = "AssistantFixture.InvalidState" }; }
        var temporary = done + ".tmp";
        try
        {
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { await JsonSerializer.SerializeAsync(output, response, Json, ct); await output.FlushAsync(ct); }
            File.Move(temporary, done);
        }
        finally { File.Delete(temporary); }
        return true;
    }

    private async Task<object> ExecuteAsync(AssistantBrowserRequest request, CancellationToken ct)
    {
        if (request.Kind == "providerCounters")
        {
            var metrics = _services.GetRequiredService<AssistantFakeChatMetrics>();
            return new { calls = metrics.Calls, titleCalls = metrics.TitleCalls, toolCycles = metrics.ToolCycles };
        }
        await using var scope = _services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var email = Email.Create(request.Args.GetProperty("email").GetString()).Value.Value;
        var userId = await db.LoginMethods.Where(row => row.Value == email && row.VerifiedAtUtc != null)
            .Select(row => row.UserId).Distinct().SingleAsync(ct);
        var access = request.Args.GetProperty("access").GetString() switch
        { "business" => Access.Business, "platform" => Access.Platform, _ => Access.Consumer };
        Guid? tenantId = null;
        if (access != Access.Platform)
        {
            var kind = access == Access.Business ? TenantKind.Business : TenantKind.Personal;
            var accesses = await services.GetRequiredService<IUserTenantAccessReader>().ListForUserAsync(userId, ct);
            var eligible = accesses.Where(row => row.Kind == kind && row.MemberStatus == MemberStatus.Active
                && row.TenantStatus == TenantStatus.Active);
            if (request.Args.TryGetProperty("tenantId", out var selected)) eligible = eligible.Where(row => row.TenantId == selected.GetGuid());
            tenantId = eligible.Single().TenantId;
        }
        else if (!await db.Users.AnyAsync(row => row.Id == userId && row.IsPlatformOperator, ct))
            throw new InvalidOperationException("Exclusive platform actor required.");
        using var lease = tenantId is { } id ? services.GetRequiredService<ITenantScope>().Enter(id) : null;
        var binding = new AssistantFinancialBinding(userId, access, tenantId);
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        if (request.Kind == "clock")
        {
            await WriteAsync(services, async token =>
            {
                if (access == Access.Platform)
                    foreach (var chat in await db.Set<PlatformAssistantConversation>().Where(row => row.OwnerUserId == userId && !row.IsSaved).ToArrayAsync(token)) chat.Touch(nowUtc.AddDays(-3651));
                else
                    foreach (var chat in await db.Set<AssistantConversation>().Where(row => row.OwnerUserId == userId && row.Access == access && !row.IsSaved).ToArrayAsync(token)) chat.Touch(nowUtc.AddDays(-3651));
            }, ct);
            var retention = services.GetRequiredService<IAssistantRetentionRepository>();
            var cutoffUtc = nowUtc.AddDays(-await retention.RetentionDaysAsync(ct));
            foreach (var candidate in (await retention.ListExpiredAsync(cutoffUtc, null, ct)).Where(row => row.OwnerUserId == userId && row.Access == access))
            {
                var result = await services.GetRequiredService<IAssistantRetentionService>().PurgeAsync(candidate, cutoffUtc, ct);
                if (result.IsFailure) throw new InvalidOperationException("Retention failed.");
            }
            return new { };
        }
        if (request.Kind == "prepare")
        {
            await WriteAsync(services, async token =>
            {
                var settings = await db.Set<AssistantPlatformSettings>().SingleAsync(token);
                var model = await db.Set<AssistantModelPrice>().Where(row => row.IsEnabled).OrderBy(row => row.InputPrice.Amount).FirstAsync(token);
                settings.Update(model.Id, [model.Id], settings.Effort, 50, Usd(1000m), true, 30);
                settings.ReplaceKey(services.GetRequiredService<IAssistantKeyProtector>().ProtectPlatform("isolated-assistant-provider-key"), "-key");
                services.GetRequiredService<ISecurityEventRepository>().Add(SecurityEvent.ForPlatformSettings(AuditActorKind.System, Guid.Empty, "Isolated assistant scenario", nowUtc));
                if (access == Access.Business)
                {
                    var feature = await db.TenantFeatures.SingleOrDefaultAsync(row => row.TenantId == tenantId && row.FeatureKey == AssistantFeature.Key, token);
                    if (feature is null) db.Add(TenantFeature.Create(tenantId!.Value, AssistantFeature.Key, true, TenantFeatureSource.Manual, null).Value);
                    else feature.Update(true, TenantFeatureSource.Manual, null);
                    services.GetRequiredService<ISecurityEventRepository>().Add(SecurityEvent.ForTenantFeatures(AuditActorKind.System, Guid.Empty, tenantId!.Value, "Isolated assistant scenario", nowUtc));
                    var policy = await db.Set<AssistantOrganizationPolicy>().SingleOrDefaultAsync(row => row.OrganizationId == tenantId, token);
                    if (policy is null) { policy = AssistantOrganizationPolicy.Create(tenantId.Value, Usd(1000m)); db.Add(policy); }
                    policy.SetAllowOwnKey(false);
                    var own = await db.Set<AssistantOrganizationSettings>().SingleOrDefaultAsync(token);
                    if (own is null) { own = AssistantOrganizationSettings.Create(); db.Add(own); }
                    own.Update("PlatformKey", model.Id, settings.Effort, 50, Usd(1000m), 30);
                    own.ReplaceKey(null, null);
                }
            }, ct);
            if (tenantId is { } cacheId) await services.GetRequiredService<ITenantFeatureCache>().InvalidateAsync(cacheId, ct);
            return new { configured = true };
        }
        var alerts = await AssistantQuotaFixture.StageAsync(services, binding, request.Args,
            _fixtures.GetValueOrDefault(binding), ct);
        _fixtures[binding] = alerts.OperationId;
        foreach (var alertId in alerts.Alerts)
            if ((await services.GetRequiredService<IAssistantBudgetAlertService>().RetryAsync(alertId, ct)).IsFailure)
                throw new InvalidOperationException("Alert failed.");
        return request.Args.TryGetProperty("notify", out var notify) && notify.GetBoolean()
            ? new { alertQueued = alerts.Alerts.Length > 0 } : (object)new { };
    }

    internal static async Task WriteAsync(IServiceProvider services, Func<CancellationToken, Task> work, CancellationToken ct)
    {
        var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async token =>
        { await work(token); return Result.Success(); }, CommitPolicy.OnSuccess, ct);
        if (result.IsFailure) throw new InvalidOperationException("Isolated assistant preparation failed.");
    }
    internal static Money Usd(decimal amount) => new(amount, CurrencyCode.Create("USD").Value);
}
