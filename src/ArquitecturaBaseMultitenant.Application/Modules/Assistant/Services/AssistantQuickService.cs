using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Time;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

/// <summary>Executes tools and renders locally. It does not claim HTTP Quick persistence or replay.</summary>
internal sealed class AssistantQuickService(AssistantToolResolver tools, AssistantQuickResponseBuilder responses,
    IProfileService profiles, IRoleService roles, ITimeZoneService zones, TimeProvider clock,
    ILogger<AssistantQuickService> logger) : IAssistantQuickService
{
    public Task<Result<IReadOnlyList<AssistantQuickQueryResponse>>> ListAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync<IReadOnlyList<AssistantQuickQueryResponse>>(logger, clock, "AssistantQuickList", async () =>
        {
            var available = new List<AssistantQuickQueryResponse>();
            foreach (var query in AssistantQuickCatalog.All)
                if (await tools.IsAvailableAsync(query.ToolKey, cancellationToken)) available.Add(new AssistantQuickQueryResponse(query.Key, query.LabelKey));
            return available;
        });

    public Task<Result<AssistantQuickCompositionResponse>> ExecuteAsync(string key, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<AssistantQuickCompositionResponse>(logger, clock, "AssistantQuickExecute", async () =>
        {
            var query = AssistantQuickCatalog.All.SingleOrDefault(item => item.Key == key);
            if (query is null || !await tools.IsAvailableAsync(query.ToolKey, cancellationToken)) return AssistantErrors.QuickQueryNotAvailable;
            var profile = await profiles.GetAsync(cancellationToken);
            if (profile.IsFailure) return profile.Error;
            if (profile.Value.Access != query.Access) return AssistantErrors.QuickQueryNotAvailable;
            var args = await ArgumentsAsync(key, profile.Value.TimeZoneId, cancellationToken);
            if (args.IsFailure) return args.Error;
            var data = await tools.ExecuteAsync(query.ToolKey, args.Value, cancellationToken);
            if (data.IsFailure) return data.Error;
            if (data.Value is not AssistantQuickToolData quick) return AssistantErrors.QuickQueryNotAvailable;
            var responseData = quick.Data;
            if (key == "my_signin")
            {
                var help = await tools.ExecuteAsync("help_article", JsonSerializer.SerializeToElement(new { topicKey = "identity.signin" }), cancellationToken);
                if (help.IsFailure) return help.Error;
                if (help.Value is not AssistantHelpToolData articles || responseData is not AssistantAccountQuickDataResponse account)
                    return AssistantErrors.QuickQueryNotAvailable;
                responseData = account with { Articles = articles.Articles };
            }
            if (!await tools.IsAvailableAsync(query.ToolKey, cancellationToken)) return AssistantErrors.QuickQueryNotAvailable;
            return await responses.BuildAsync(query, responseData, profile.Value.Culture, profile.Value.TimeZoneId, cancellationToken);
        });

    private async Task<Result<JsonElement>> ArgumentsAsync(string key, string zone, CancellationToken ct)
    {
        switch (key)
        {
            case "active_users": return JsonSerializer.SerializeToElement(new { status = "Active", page = 1, pageSize = 20 });
            case "expired_invitations": return JsonSerializer.SerializeToElement(new { status = "Expired", page = 1, pageSize = 20 });
            case "owners":
                var owners = await roles.ListAsync(new ListRolesRequest { SystemRoleKey = SystemRoles.TenantAdmin, Page = 1, PageSize = 20 }, ct);
                if (owners.IsFailure) return owners.Error;
                if (owners.Value.Items.Count != 1) return AssistantErrors.QuickQueryNotAvailable;
                return JsonSerializer.SerializeToElement(new { roleId = owners.Value.Items[0].Id, page = 1, pageSize = 20 });
            case "recent_changes": return JsonSerializer.SerializeToElement(new { });
            case "pending_organizations": return JsonSerializer.SerializeToElement(new { status = "PendingApproval", page = 1, pageSize = 20 });
            case "pending_recoveries": return JsonSerializer.SerializeToElement(new { status = "Pending", page = 1, pageSize = 20 });
            case "today_security":
                var nowUtc = clock.GetUtcNow().UtcDateTime;
                var range = zones.GetDayRangeUtc(DateOnly.FromDateTime(zones.ConvertToLocal(nowUtc, zone)), zone);
                return JsonSerializer.SerializeToElement(new { fromUtc = range.StartUtc, toUtc = range.EndUtc });
            case "assistant_usage":
                var month = DateOnly.FromDateTime(zones.ConvertToLocal(clock.GetUtcNow().UtcDateTime, zone));
                return JsonSerializer.SerializeToElement(new { month = JsonSerializer.SerializeToElement(month).GetString()![..7], page = 1, pageSize = 20 });
            case "my_messages": return JsonSerializer.SerializeToElement(new { page = 1, pageSize = 20 });
            default: return JsonSerializer.SerializeToElement(new { });
        }
    }
}
