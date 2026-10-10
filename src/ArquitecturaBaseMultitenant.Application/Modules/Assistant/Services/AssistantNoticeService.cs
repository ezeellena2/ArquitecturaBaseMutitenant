using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantNoticeService(AssistantNoticePolicy policy, IRequestValidator validator,
    TimeProvider clock, ILogger<AssistantNoticeService> logger) : IAssistantNoticeService
{
    public Task<Result> CheckAcceptedAsync(AssistantNoticeRequest request, CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "AssistantNoticeCheckAccepted", async () =>
        {
            if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
            return await policy.CheckAcceptedAsync(request.NoticeVersion, ct);
        });
}
