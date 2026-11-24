using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Retention;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Invitations;

/// <summary>
/// ADR 0062: las invitaciones cerradas (aceptadas, revocadas o vencidas) de la organización activa se borran a los 30
/// días, salvo la última de cada destino de cada persona invitada, y nunca una pendiente. Conservar la última de cada
/// persona deja que la auditoría de la organización siga nombrando por su correo a quien no tiene nombre. Corre en el
/// alcance de la organización, con RLS, un lote por transacción.
/// </summary>
internal sealed class InvitationRetentionService(
    IInvitationRetentionRepository retention,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<InvitationRetentionService> logger) : IInvitationRetentionService
{
    public Task<Result<RetentionBatch>> PurgeClosedAsync(RetentionCursor? after, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "PurgeClosedInvitations", () =>
            unitOfWork.ExecuteInTransactionAsync(async ct => Result.Success(await retention.DeleteClosedAsync(
                    IdentityRetentionPolicy.InvitationsClosedBeforeUtc(timeProvider.GetUtcNow().UtcDateTime,
                        IdentityRetentionPeriods.Default), after, IdentityRetentionPolicy.BatchSize, ct)),
                CommitPolicy.OnSuccess, cancellationToken));
}
