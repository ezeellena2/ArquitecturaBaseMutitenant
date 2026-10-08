using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Identity;

/// <summary>Coordina alta, verificación, elección del principal y baja de métodos de ingreso con validación y una UnitOfWork por cambio.</summary>
internal sealed class LoginMethodManagementService(ICurrentUser currentUser, LoginMethodIssuer issuer,
    LoginMethodVerifier verifier, LoginMethodEditor changer, IRequestValidator validator, IUnitOfWork unitOfWork,
    TimeProvider timeProvider, ILogger<LoginMethodManagementService> logger) : ILoginMethodManagementService
{
    public Task<Result<LoginMethodCodeResponse>> AddEmailAsync(AddLoginEmailRequest request, CancellationToken ct) =>
        OperationLog.RunAsync<LoginMethodCodeResponse>(logger, timeProvider, "AddLoginEmail", () => AddEmailCoreAsync(request, ct));

    public Task<Result<LoginMethodCodeResponse>> AddAsync(AddLoginMethodRequest request, CancellationToken ct) =>
        OperationLog.RunAsync<LoginMethodCodeResponse>(logger, timeProvider, "AddLoginMethod", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
            var destination = issuer.Resolve(request);
            if (destination.IsFailure) return destination.Error;
            if (currentUser.UserId is not { } userId) return UserErrors.NotFound;
            return await unitOfWork.ExecuteInTransactionAsync(token => issuer.AddAsync(userId, destination.Value, token), CommitPolicy.OnSuccess, ct);
        });

    private async Task<Result<LoginMethodCodeResponse>> AddEmailCoreAsync(AddLoginEmailRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
        if (currentUser.UserId is not { } userId) return UserErrors.NotFound;
        var destination = issuer.Resolve(new AddLoginMethodRequest(request.Email));
        if (destination.IsFailure) return destination.Error;
        return await unitOfWork.ExecuteInTransactionAsync(token => issuer.AddAsync(userId, destination.Value, token),
            CommitPolicy.OnSuccess, ct);
    }

    public Task<Result<LoginMethodCodeResponse>> SendCodeAsync(Guid methodId, CancellationToken ct) =>
        OperationLog.RunAsync<LoginMethodCodeResponse>(logger, timeProvider, "SendLoginMethodCode", async () =>
        {
            if (currentUser.UserId is not { } userId) return UserErrors.NotFound;
            return await unitOfWork.ExecuteInTransactionAsync(token => issuer.SendAsync(userId, methodId, token),
                CommitPolicy.OnAnyResult, ct);
        });

    public Task<Result> VerifyAsync(VerifyLoginMethodRequest request, CancellationToken ct) =>
        OperationLog.RunAsync(logger, timeProvider, "VerifyLoginMethod", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
            if (currentUser.UserId is not { } userId) return UserErrors.NotFound;
            var isPhone = false;
            try
            {
                return await unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    isPhone = await verifier.IsPhoneAsync(userId, request.MethodId, token);
                    return await verifier.VerifyAsync(userId, request, token);
                }, CommitPolicy.OnAnyResult, ct);
            }
            catch (UniqueConstraintViolationException exception)
                when (exception.ConstraintName == UniqueKeys.VerifiedLoginMethod)
            {
                return FieldErrors.On(isPhone ? LoginMethodErrors.PhoneAlreadyUsed : LoginMethodErrors.AlreadyUsed, isPhone ? "phone" : "email");
            }
        });

    public Task<Result> MakePrimaryAsync(ChangeLoginMethodRequest request, CancellationToken ct) =>
        OperationLog.RunAsync(logger, timeProvider, "MakeLoginMethodPrimary", () => ChangeAsync(request, false, ct));

    public Task<Result> RemoveAsync(ChangeLoginMethodRequest request, CancellationToken ct) =>
        OperationLog.RunAsync(logger, timeProvider, "RemoveLoginMethod", () => ChangeAsync(request, true, ct));

    private async Task<Result> ChangeAsync(ChangeLoginMethodRequest request, bool remove, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
        if (currentUser.UserId is not { } userId) return UserErrors.NotFound;
        return await unitOfWork.ExecuteInTransactionAsync(token => changer.ChangeAsync(userId, request, remove, token),
            CommitPolicy.OnSuccess, ct);
    }
}
