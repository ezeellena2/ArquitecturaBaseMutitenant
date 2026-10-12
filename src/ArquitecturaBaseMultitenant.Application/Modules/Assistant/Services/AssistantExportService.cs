using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantExportService(ICurrentUser user, AssistantConversationPolicy policy, IAssistantConversationReader reader,
    IAssistantExportRenderer renderer, IRequestValidator validator, TimeProvider clock, ILogger<AssistantExportService> logger) : IAssistantExportService
{
    public Task<Result<AssistantFileResponse>> ExportAsync(Guid id, AssistantExportFormat? format, CancellationToken ct) =>
        OperationLog.RunAsync<AssistantFileResponse>(logger, clock, "AssistantConversationExport", async () =>
        {
            if (await validator.ValidateAsync(new AssistantExportRequest(format), ct) is { } invalid) return invalid;
            var allowed = await policy.AuthorizeAsync(ct);
            if (allowed.IsFailure) return allowed.Error;
            var conversation = await reader.FindByIdAsync(user.UserId!.Value, user.Access!.Value, id, ct);
            if (conversation is null) return AssistantErrors.ConversationNotFound;
            var messages = new List<AssistantMessageResponse>();
            string? cursor = null;
            do
            {
                var page = await reader.ListMessagesAsync(user.UserId.Value, user.Access.Value, id, new AssistantMessageListRequest(cursor, 100), ct);
                messages.AddRange(page.Items);
                cursor = page.NextCursor;
            } while (cursor is not null);
            var filtered = await policy.FilterHistoryLinksAsync(messages, ct);
            if (filtered.IsFailure) return filtered.Error;
            allowed = await policy.AuthorizeAsync(ct);
            if (allowed.IsFailure) return allowed.Error;
            if (await reader.FindByIdAsync(user.UserId.Value, user.Access.Value, id, ct) is null) return AssistantErrors.ConversationNotFound;
            return renderer.Render(conversation, filtered.Value, format ?? AssistantExportFormat.Markdown);
        });
}
