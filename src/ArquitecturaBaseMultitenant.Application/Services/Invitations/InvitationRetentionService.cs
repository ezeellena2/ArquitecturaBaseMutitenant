using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Retention;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Invitations;

/// <summary>
/// ADR 0062: las invitaciones cerradas (aceptadas, revocadas o vencidas) de la organización activa se borran a los días
/// que fija Plataforma (272, 30 por defecto), salvo la última de cada destino de cada persona invitada, y nunca una pendiente. Conservar la última de cada
/// persona deja que la auditoría de la organización siga nombrando por su correo a quien no tiene nombre. Corre en el
/// alcance de la organización, con RLS, un lote por transacción.
/// </summary>
internal sealed class InvitationRetentionService(
    IInvitationRetentionRepository retention,
    IPlatformSettingsReader settings,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<InvitationRetentionService> logger) : IInvitationRetentionService
{
    public Task<Result<RetentionBatch>> PurgeClosedAsync(RetentionCursor? after, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "PurgeClosedInvitations", async () =>
        {
            // 272: el plazo sale de «Privacidad y retención» de Plataforma en cada lote.
            var periods = await settings.FindRetentionPeriodsAsync(cancellationToken);
            return await unitOfWork.ExecuteInTransactionAsync(async ct => Result.Success(await retention.DeleteClosedAsync(
                    IdentityRetentionPolicy.InvitationsClosedBeforeUtc(timeProvider.GetUtcNow().UtcDateTime, periods), after,
                    IdentityRetentionPolicy.BatchSize, ct)),
                CommitPolicy.OnSuccess, cancellationToken);
        });
}
