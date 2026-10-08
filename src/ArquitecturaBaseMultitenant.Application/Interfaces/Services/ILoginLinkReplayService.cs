using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;
namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;
/// <summary>Revalidación de una operación ya confirmada, sin consumir, auditar ni crear sesión.</summary>
public interface ILoginLinkReplayService
{
    Task<Result> ValidateAsync(RedeemLoginLinkRequest request, LoginLinkReplayOutcome outcome, CancellationToken ct);
}
public enum LoginLinkReplayOutcome { SignedIn, PendingDeletion, Rejected }
