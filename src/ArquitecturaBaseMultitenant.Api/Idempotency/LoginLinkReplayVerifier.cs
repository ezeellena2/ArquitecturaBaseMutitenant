using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.Contracts.Auth;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
namespace ArquitecturaBaseMultitenant.Api.Idempotency;
internal sealed class LoginLinkReplayVerifier(ILoginLinkReplayService service) : IIdempotencyReplayVerifier
{
    public async Task<Result> ValidateAsync(HttpRequest request, int originalStatusCode, string? originalBody, CancellationToken ct)
    {
        request.Body.Position = 0;
        try
        {
            var body = await JsonSerializer.DeserializeAsync<RedeemLoginLinkHttpRequest>(request.Body,
                JsonSerializerOptions.Web, ct);
            var outcome = LoginLinkReplayOutcome.Rejected;
            if (originalStatusCode is >= 200 and < 300) outcome = LoginLinkReplayOutcome.SignedIn;
            else if (originalBody is not null)
            {
                using var cached = JsonDocument.Parse(originalBody);
                if (cached.RootElement.TryGetProperty("code", out var code) && code.GetString() == AccountErrors.PendingDeletion.Code)
                    outcome = LoginLinkReplayOutcome.PendingDeletion;
            }
            return body is null ? Result.Failure(LoginLinkErrors.Invalid)
                : await service.ValidateAsync(new RedeemLoginLinkRequest(body.Token, body.Access), outcome, ct);
        }
        catch (JsonException) { return Result.Failure(LoginLinkErrors.Invalid); }
        finally { request.Body.Position = 0; }
    }
}
