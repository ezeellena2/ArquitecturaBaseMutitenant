using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantNoticeWriter(ICurrentUser user, IAssistantNoticeRepository repository,
    AssistantNoticePolicy policy, TimeProvider clock)
{
    internal async Task<Result> AcceptAsync(AssistantNoticeRequest request, CancellationToken ct)
    {
        if (user.UserId is not { } owner || user.Access is not { } access) return UserErrors.NotFound;
        await repository.LockAsync(owner, ct);
        var allowed = await policy.CheckVersionAsync(request.NoticeVersion, ct);
        if (allowed.IsFailure) return allowed;
        if (!await policy.IsAcceptedAsync(owner, request.NoticeVersion!, ct))
            repository.Add(AssistantNoticeAcceptance.Create(owner, access, request.NoticeVersion!, clock.GetUtcNow().UtcDateTime));
        return Result.Success();
    }
}
