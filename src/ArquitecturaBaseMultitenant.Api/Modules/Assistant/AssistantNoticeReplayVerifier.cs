using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Api.Modules.Assistant.Contracts;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant;

internal sealed class AssistantNoticeReplayVerifier(IAssistantNoticeService service, IOptions<JsonOptions> json) : IIdempotencyReplayVerifier
{
    public async Task<Result> ValidateAsync(HttpRequest request, int originalStatusCode, string? originalBody, CancellationToken ct)
    {
        request.Body.Position = 0;
        try
        {
            var body = await JsonSerializer.DeserializeAsync<AssistantNoticeHttpRequest>(request.Body, json.Value.JsonSerializerOptions, ct);
            return body is null ? Result.Failure(AssistantErrors.NoticeAcceptanceRequired)
                : await service.CheckAcceptedAsync(new AssistantNoticeRequest(body.NoticeVersion), ct);
        }
        catch (JsonException) { return Result.Failure(AssistantErrors.NoticeAcceptanceRequired); }
        finally { request.Body.Position = 0; }
    }
}
