using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Models.Profile;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Profile;

/// <summary>Lee el perfil efectivo del acceso actual y actualiza preferencias globales de la cuenta con control de versión.</summary>
internal sealed class ProfileService(ICurrentUser currentUser, ITenantContext tenantContext,
    ProfileSnapshotBuilder snapshots, IUserRepository users, IRequestValidator validator,
    IUnitOfWork unitOfWork, TimeProvider timeProvider, ILogger<ProfileService> logger) : IProfileService
{
    public Task<Result<MeResponse>> GetAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync<MeResponse>(logger, timeProvider, "GetProfile", async () =>
        {
            if (currentUser.UserId is not { } userId || currentUser.Access is not { } access)
            {
                return UserErrors.NotFound;
            }

            return await snapshots.BuildAsync(userId, access, tenantContext.TenantId, cancellationToken)
                is { } profile ? profile : UserErrors.NotFound;
        });

    public Task<Result<VersionResponse>> UpdateAsync(UpdateMeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return OperationLog.RunAsync<VersionResponse>(logger, timeProvider, "UpdateProfile", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid)
            {
                return invalid;
            }

            // La versión del perfil es un contador propio, no el xmin de la cuenta: la que quedó es la que subió este
            // guardado, válida una vez hecho el commit.
            var saved = await unitOfWork.ExecuteInTransactionAsync(
                ct => UpdateCoreAsync(request, ct), CommitPolicy.OnSuccess, cancellationToken);
            return saved.IsSuccess ? new VersionResponse(saved.Value) : saved.Error;
        });
    }

    private async Task<Result<uint>> UpdateCoreAsync(UpdateMeRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId || await users.GetByIdAsync(userId, cancellationToken) is null)
        {
            return UserErrors.NotFound;
        }

        return await users.UpdateProfileAsync(userId, request.DisplayName, request.Culture!,
            request.TimeZoneId!, request.Version!.Value, cancellationToken);
    }
}
