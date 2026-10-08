using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Puerto del núcleo dentro de la UoW del llamador. La persistencia del enlace se completa en T07.</summary>
internal sealed class LoginLinkIssuer(IUserRepository users, ILoginMethodRepository methods) : ILoginLinkIssuer
{
    public async Task<Result<IssuedLoginLink>> IssueAsync(Guid userId, Guid methodId, CancellationToken ct)
    {
        await methods.LockUserAsync(userId, ct);
        var account = await users.GetByIdAsync(userId, ct);
        if (account is null || account.IsPlatformOperator) return LoginLinkErrors.Invalid;
        return ContractErrors.NotImplemented;
    }

    public Task<Result> InvalidateForMethodAsync(Guid userId, Guid methodId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(Result.Failure(ContractErrors.NotImplemented));
    }
}
