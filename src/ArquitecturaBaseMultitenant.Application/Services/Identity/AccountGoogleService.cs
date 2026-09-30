using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Identity;

/// <summary>
/// Vincula Google como método de ingreso de una cuenta que ya tiene sesión.
/// Comprueba que el callback corresponda a esa cuenta y que Google haya verificado el correo;
/// las reglas del vínculo se ejecutan dentro de una transacción.
/// </summary>
internal sealed class AccountGoogleService(ISignInService signIn, GoogleMethodLinker linker,
    IUnitOfWork unitOfWork, TimeProvider timeProvider, ILogger<AccountGoogleService> logger) : IAccountGoogleService
{
    public Task<Result> LinkAsync(LinkGoogleRequest request, CancellationToken ct) =>
        OperationLog.RunAsync(logger, timeProvider, "LinkAccountGoogle", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var login = await signIn.GetExternalLoginAsync(ct);
            await signIn.SignOutExternalAsync(ct);
            if (request.ExpectedUserId == Guid.Empty || request.ExpectedUserId != request.SessionUserId
                || login is null || login.Provider != "Google" || string.IsNullOrWhiteSpace(login.ProviderKey))
                return ExternalLoginErrors.Failed;
            if (!login.EmailVerified || login.Email is null) return ExternalLoginErrors.EmailNotVerified;
            return await unitOfWork.ExecuteInTransactionAsync(token => linker.LinkAsync(request.ExpectedUserId, login, token),
                CommitPolicy.OnSuccess, ct);
        });
}
